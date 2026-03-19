using Messaging.RabbitMq.Configuration;
using Messaging.RabbitMq.Extensions;
using Messaging.RabbitMq.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.RabbitMq.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions"/>.
/// Verifies that the DI registration and resilience configuration work correctly.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    private static IConfiguration CreateTestConfiguration()
    {
        var configData = new Dictionary<string, string?>
        {
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Port"] = "5672",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Username"] = "guest",
            ["RabbitMq:Password"] = "guest"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public void AddRabbitMqMessaging_ShouldRegisterNotificationEventPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();

        // Act
        services.AddRabbitMqMessaging(configuration);

        // Assert — INotificationEventPublisher should be registered as scoped
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(INotificationEventPublisher));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
        descriptor.ImplementationType.Should().Be(typeof(NotificationEventPublisher));
    }

    [Fact]
    public void AddRabbitMqMessaging_WithConnectionString_ShouldRegisterNotificationEventPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddRabbitMqMessaging("amqp://guest:guest@localhost:5672/");

        // Assert
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(INotificationEventPublisher));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddRabbitMqMessaging_WithNullConfig_ShouldUseDefaultRabbitMqConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act — should not throw even with empty config (falls back to defaults)
        var act = () => services.AddRabbitMqMessaging(emptyConfig);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddRabbitMqMessaging_WithCustomResilienceOptions_ShouldAcceptOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        var customOptions = new ResilienceOptions
        {
            EnableCircuitBreaker = false,
            CircuitBreakerTripThreshold = 10,
            RetryIntervals = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8)]
        };

        // Act — should not throw with custom resilience options
        var act = () => services.AddRabbitMqMessaging(
            configuration, resilienceOptions: customOptions);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddRabbitMqMessaging_WithConsumers_ShouldInvokeConsumerConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();
        var consumerConfigInvoked = false;

        // Act
        services.AddRabbitMqMessaging(configuration, cfg =>
        {
            consumerConfigInvoked = true;
        });

        // Assert
        consumerConfigInvoked.Should().BeTrue();
    }

    [Fact]
    public void AddRabbitMqMessaging_ConnectionStringOverload_WithCustomResilience_ShouldAcceptOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var customOptions = new ResilienceOptions
        {
            EnableCircuitBreaker = true,
            CircuitBreakerTripThreshold = 3,
            CircuitBreakerActiveDuration = TimeSpan.FromSeconds(30),
            UseInMemoryOutbox = false
        };

        // Act
        var act = () => services.AddRabbitMqMessaging(
            "amqp://guest:guest@localhost:5672/",
            resilienceOptions: customOptions);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void AddRabbitMqMessaging_WithNullResilienceOptions_ShouldUseDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = CreateTestConfiguration();

        // Act — null resilience options should use defaults
        var act = () => services.AddRabbitMqMessaging(
            configuration, resilienceOptions: null);

        // Assert
        act.Should().NotThrow();
    }
}
