namespace Messaging.RabbitMq.RawJson;

/// <summary>
/// Options for a receive endpoint that consumes RAW JSON messages — a bare JSON body with no
/// MassTransit envelope and no message-type header, as produced by a plain AMQP publisher
/// (for example NestJS <c>amqplib</c>).
/// </summary>
public sealed class RawJsonReceiveEndpointOptions
{
    /// <summary>
    /// The default exchange type used by <see cref="BindToExchange"/>.
    /// </summary>
    public const string TopicExchangeType = "topic";

    private readonly List<RawJsonExchangeBinding> _bindings = [];

    /// <summary>
    /// Exchange bindings applied to the endpoint's queue.
    /// </summary>
    public IReadOnlyList<RawJsonExchangeBinding> Bindings => _bindings;

    /// <summary>
    /// Binds the endpoint's queue to an existing (or idempotently declared) exchange with a routing key.
    /// The exchange is declared durable with the given type; both sides of a contract may declare it.
    /// </summary>
    /// <param name="exchangeName">The exchange name, e.g. <c>proovid.verification</c>.</param>
    /// <param name="routingKey">The routing key, e.g. <c>verification.completed</c>.</param>
    /// <param name="exchangeType">The exchange type. Defaults to <c>topic</c>.</param>
    /// <returns>The options, for chaining.</returns>
    public RawJsonReceiveEndpointOptions BindToExchange(
        string exchangeName,
        string routingKey,
        string exchangeType = TopicExchangeType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName);
        ArgumentNullException.ThrowIfNull(routingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeType);

        _bindings.Add(new RawJsonExchangeBinding(exchangeName, routingKey, exchangeType));
        return this;
    }
}

/// <summary>
/// A single exchange binding for a raw JSON receive endpoint.
/// </summary>
/// <param name="ExchangeName">The exchange name.</param>
/// <param name="RoutingKey">The routing key.</param>
/// <param name="ExchangeType">The exchange type (e.g. <c>topic</c>).</param>
public sealed record RawJsonExchangeBinding(string ExchangeName, string RoutingKey, string ExchangeType);
