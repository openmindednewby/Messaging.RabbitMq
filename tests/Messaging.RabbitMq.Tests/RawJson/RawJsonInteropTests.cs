using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MassTransit;
using Messaging.RabbitMq.Configuration;
using Messaging.RabbitMq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Messaging.RabbitMq.Tests.RawJson;

/// <summary>
/// Starts one RabbitMQ container for all raw JSON interop tests.
/// </summary>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:3-management").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public sealed record InboundVerification
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("tenant_id")]
    public string TenantId { get; init; } = string.Empty;
}

public sealed record ScreeningReply
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

public sealed class NonRetryableTestException(string message) : Exception(message);

public sealed class TransientTestException(string message) : Exception(message);

public sealed class RawJsonProbe
{
    private int _attempts;

    public TaskCompletionSource<InboundVerification> Received { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Attempts => _attempts;

    public void RecordAttempt() => Interlocked.Increment(ref _attempts);
}

public sealed class ReplyingConsumer(RawJsonProbe probe) : IConsumer<InboundVerification>
{
    public async Task Consume(ConsumeContext<InboundVerification> context)
    {
        probe.RecordAttempt();
        await context.Publish(new ScreeningReply { RequestId = context.Message.RequestId, Status = "screened" });
        probe.Received.TrySetResult(context.Message);
    }
}

public sealed class NonRetryableConsumer(RawJsonProbe probe) : IConsumer<InboundVerification>
{
    public Task Consume(ConsumeContext<InboundVerification> context)
    {
        probe.RecordAttempt();
        throw new NonRetryableTestException("unknown tenant");
    }
}

public sealed class TransientConsumer(RawJsonProbe probe) : IConsumer<InboundVerification>
{
    public Task Consume(ConsumeContext<InboundVerification> context)
    {
        probe.RecordAttempt();
        throw new TransientTestException("vendor unavailable");
    }
}

/// <summary>
/// Integration tests against a real broker: a plain <c>RabbitMQ.Client</c> publisher stands in for NestJS amqplib.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RawJsonInteropTests(RabbitMqContainerFixture fixture) : IClassFixture<RabbitMqContainerFixture>
{
    private const string Exchange = "proovid.verification";
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(200);
    private const int RetryCount = 3;

    [Theory]
    [InlineData(null)]
    [InlineData("application/json")]
    public async Task AddRawJsonConsumer_WhenBodyHasNoEnvelopeOrTypeHeader_ConsumesMessage(string? contentType)
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var routingKey = $"verification.completed.{suffix}";
        await using var host = await StartBusAsync(x =>
            x.AddRawJsonConsumer<ReplyingConsumer>($"aml.verification-completed.{suffix}",
                e => e.BindToExchange(Exchange, routingKey)));

        // Act
        await PublishRawAsync(routingKey, """{"request_id":"req-1","tenant_id":"t-1"}""", contentType);

        // Assert
        var message = await host.Probe.Received.Task.WaitAsync(WaitTimeout);
        message.RequestId.Should().Be("req-1");
        message.TenantId.Should().Be("t-1");
    }

    [Fact]
    public async Task AddRawJsonPublisher_WhenConsumerReplies_PublishesBodyWithoutEnvelope()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var inboundKey = $"verification.completed.{suffix}";
        var replyKey = $"verification.screening.completed.{suffix}";
        var replyQueue = $"module-b.verification-screening-completed.{suffix}";

        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);
        await channel.QueueDeclareAsync(replyQueue, durable: true, exclusive: false, autoDelete: false);
        await channel.QueueBindAsync(replyQueue, Exchange, replyKey);

        await using var host = await StartBusAsync(x =>
        {
            x.AddRawJsonConsumer<ReplyingConsumer>($"aml.verification-completed.{suffix}",
                e => e.BindToExchange(Exchange, inboundKey));
            x.AddRawJsonPublisher<ScreeningReply>(Exchange, replyKey);
        });

        // Act
        await PublishRawAsync(inboundKey, """{"request_id":"req-2","tenant_id":"t-2"}""", "application/json");

        // Assert
        var reply = await WaitForMessageAsync(channel, replyQueue);
        reply.BasicProperties.ContentType.Should().Be("application/json");
        var body = Encoding.UTF8.GetString(reply.Body.Span);
        using var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("messageType", out _).Should().BeFalse("a raw body has no envelope: {0}", body);
        json.RootElement.TryGetProperty("message", out _).Should().BeFalse("a raw body has no envelope: {0}", body);
        json.RootElement.TryGetProperty("request_id", out var requestId).Should().BeTrue("snake_case is the wire format: {0}", body);
        requestId.GetString().Should().Be("req-2");
        json.RootElement.GetProperty("status").GetString().Should().Be("screened");
    }

    [Fact]
    public async Task NonRetryableExceptions_WhenConsumerThrowsIgnoredType_MovesToErrorQueueWithoutRetry()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N");
        var routingKey = $"verification.completed.{suffix}";
        var queue = $"aml.verification-completed.{suffix}";
        var resilience = FastResilience();
        resilience.NonRetryableExceptions = [typeof(NonRetryableTestException)];

        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync($"{queue}_error", durable: true, exclusive: false, autoDelete: false);

        await using var host = await StartBusAsync(
            x => x.AddRawJsonConsumer<NonRetryableConsumer>(queue, e => e.BindToExchange(Exchange, routingKey)),
            resilience);

        // Act
        await PublishRawAsync(routingKey, """{"request_id":"req-3","tenant_id":"unknown"}""", "application/json");

        // Assert
        await WaitForMessageAsync(channel, $"{queue}_error");
        host.Probe.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryPolicy_WhenConsumerThrowsRetryableType_RetriesBeforeErrorQueue()
    {
        // Arrange — negative control: proves the attempt counter can observe retries at all
        var suffix = Guid.NewGuid().ToString("N");
        var routingKey = $"verification.completed.{suffix}";
        var queue = $"aml.verification-completed.{suffix}";
        var resilience = FastResilience();
        resilience.NonRetryableExceptions = [typeof(NonRetryableTestException)];

        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync($"{queue}_error", durable: true, exclusive: false, autoDelete: false);

        await using var host = await StartBusAsync(
            x => x.AddRawJsonConsumer<TransientConsumer>(queue, e => e.BindToExchange(Exchange, routingKey)),
            resilience);

        // Act
        await PublishRawAsync(routingKey, """{"request_id":"req-4","tenant_id":"t-4"}""", "application/json");

        // Assert
        await WaitForMessageAsync(channel, $"{queue}_error");
        host.Probe.Attempts.Should().Be(RetryCount + 1);
    }

    private static ResilienceOptions FastResilience() => new()
    {
        RetryIntervals = Enumerable.Repeat(RetryInterval, RetryCount).ToArray(),
        EnableCircuitBreaker = false
    };

    private async Task<BusHost> StartBusAsync(
        Action<IBusRegistrationConfigurator> configure,
        ResilienceOptions? resilience = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<RawJsonProbe>();
        services.AddRabbitMqMessaging(fixture.ConnectionString, configure, resilience ?? FastResilience());

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IBusControl>();
        using var cts = new CancellationTokenSource(WaitTimeout);
        await bus.StartAsync(cts.Token);

        return new BusHost(provider, bus, provider.GetRequiredService<RawJsonProbe>());
    }

    private async Task<IConnection> CreateConnectionAsync() =>
        await new ConnectionFactory { Uri = new Uri(fixture.ConnectionString) }.CreateConnectionAsync();

    private async Task PublishRawAsync(string routingKey, string body, string? contentType)
    {
        await using var connection = await CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true);

        var bytes = Encoding.UTF8.GetBytes(body);
        if (contentType is null)
        {
            // No properties at all: no content-type, no type header, no message id.
            await channel.BasicPublishAsync(Exchange, routingKey, bytes);
            return;
        }

        var properties = new BasicProperties { ContentType = contentType, Persistent = true, MessageId = Guid.NewGuid().ToString() };
        await channel.BasicPublishAsync(Exchange, routingKey, mandatory: false, properties, bytes);
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(IChannel channel, string queue)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: true);
            if (result is not null)
                return result;

            await Task.Delay(RetryInterval);
        }

        throw new TimeoutException($"No message arrived on '{queue}' within {WaitTimeout}.");
    }

    private sealed record BusHost(ServiceProvider Provider, IBusControl Bus, RawJsonProbe Probe) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Bus.StopAsync();
            await Provider.DisposeAsync();
        }
    }
}
