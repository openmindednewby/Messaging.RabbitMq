using MassTransit;
using Messaging.RabbitMq.RawJson;
using Microsoft.Extensions.DependencyInjection;

namespace Messaging.RabbitMq.Extensions;

/// <summary>
/// Opt-in raw JSON interop with plain AMQP publishers/consumers (no MassTransit envelope).
/// Call these inside the <c>configureConsumers</c> callback of <c>AddRabbitMqMessaging</c>.
/// Nothing changes for services that do not call them.
/// </summary>
public static class RawJsonRegistrationExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TConsumer"/> on its own receive endpoint that deserializes RAW JSON,
    /// including bodies with no content-type and no message-type header. The consumer is excluded from
    /// <c>ConfigureEndpoints</c>, so no envelope-based duplicate endpoint is created. Failed messages
    /// go to <c>{queueName}_error</c> after the bus retry policy.
    /// </summary>
    /// <param name="configurator">The bus registration configurator.</param>
    /// <param name="queueName">The durable queue name, e.g. <c>aml.verification-completed</c>.</param>
    /// <param name="configure">Exchange bindings for the queue.</param>
    /// <returns>The configurator, for chaining.</returns>
    public static IBusRegistrationConfigurator AddRawJsonConsumer<TConsumer>(
        this IBusRegistrationConfigurator configurator,
        string queueName,
        Action<RawJsonReceiveEndpointOptions>? configure = null)
        where TConsumer : class, IConsumer
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var options = new RawJsonReceiveEndpointOptions();
        configure?.Invoke(options);

        configurator.AddConsumer<TConsumer>().ExcludeFromConfigureEndpoints();
        configurator.AddSingleton<RawJsonReceiveEndpointRegistration>(
            new RawJsonReceiveEndpointRegistration<TConsumer>(queueName, options));

        return configurator;
    }

    /// <summary>
    /// Publishes <typeparamref name="TMessage"/> as a RAW JSON body (<c>content-type: application/json</c>,
    /// no envelope) to <paramref name="exchangeName"/> with <paramref name="routingKey"/>. Other message
    /// types keep the MassTransit envelope.
    /// </summary>
    /// <param name="configurator">The bus registration configurator.</param>
    /// <param name="exchangeName">The exchange, e.g. <c>proovid.verification</c>.</param>
    /// <param name="routingKey">The routing key, e.g. <c>verification.screening.completed</c>.</param>
    /// <param name="exchangeType">The exchange type. Defaults to <c>topic</c>.</param>
    /// <returns>The configurator, for chaining.</returns>
    public static IBusRegistrationConfigurator AddRawJsonPublisher<TMessage>(
        this IBusRegistrationConfigurator configurator,
        string exchangeName,
        string routingKey,
        string exchangeType = RawJsonReceiveEndpointOptions.TopicExchangeType)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentNullException.ThrowIfNull(routingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeType);

        configurator.AddSingleton<RawJsonPublishRegistration>(
            new RawJsonPublishRegistration<TMessage>(exchangeName, routingKey, exchangeType));

        return configurator;
    }

    /// <summary>
    /// Applies all raw JSON registrations to the bus. No-op when none were registered.
    /// </summary>
    internal static void ConfigureRawJson(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator cfg)
    {
        var publishers = context.GetServices<RawJsonPublishRegistration>().ToList();
        var endpoints = context.GetServices<RawJsonReceiveEndpointRegistration>().ToList();

        if (publishers.Count > 0 || endpoints.Count > 0)
            cfg.AddRawJsonSerializer(RawJsonReceiveEndpointRegistration.DeserializerOptions);

        foreach (var publisher in publishers)
            publisher.Configure(cfg);

        foreach (var endpoint in endpoints)
            endpoint.Configure(context, cfg);
    }
}
