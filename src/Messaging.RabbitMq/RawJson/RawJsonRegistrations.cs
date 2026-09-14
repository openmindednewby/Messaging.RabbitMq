using System.Net.Mime;
using MassTransit;

namespace Messaging.RabbitMq.RawJson;

/// <summary>
/// A raw JSON receive endpoint recorded at registration time and applied when the bus is configured.
/// </summary>
internal abstract class RawJsonReceiveEndpointRegistration
{
    /// <summary>
    /// Deserializer options: accept a body with no message-type header (<see cref="RawSerializerOptions.AnyMessageType"/>)
    /// and add transport headers to anything published as raw JSON.
    /// </summary>
    internal const RawSerializerOptions DeserializerOptions =
        RawSerializerOptions.AnyMessageType | RawSerializerOptions.AddTransportHeaders;

    public abstract void Configure(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator cfg);
}

/// <summary>
/// Raw JSON receive endpoint for <typeparamref name="TConsumer"/>.
/// </summary>
internal sealed class RawJsonReceiveEndpointRegistration<TConsumer>(
    string queueName,
    RawJsonReceiveEndpointOptions options) : RawJsonReceiveEndpointRegistration
    where TConsumer : class, IConsumer
{
    public override void Configure(IBusRegistrationContext context, IRabbitMqBusFactoryConfigurator cfg)
    {
        cfg.ReceiveEndpoint(queueName, e =>
        {
            // The publisher knows nothing about MassTransit's message-type exchanges.
            e.ConfigureConsumeTopology = false;

            // isDefault: true makes raw JSON the deserializer for messages with NO content-type header.
            e.UseRawJsonDeserializer(DeserializerOptions, isDefault: true);

            foreach (var binding in options.Bindings)
            {
                e.Bind(binding.ExchangeName, b =>
                {
                    b.ExchangeType = binding.ExchangeType;
                    b.RoutingKey = binding.RoutingKey;
                });
            }

            e.ConfigureConsumer<TConsumer>(context);
        });
    }
}

/// <summary>
/// A message type published as raw JSON, recorded at registration time and applied when the bus is configured.
/// </summary>
internal abstract class RawJsonPublishRegistration
{
    public abstract void Configure(IRabbitMqBusFactoryConfigurator cfg);
}

/// <summary>
/// Publishes <typeparamref name="TMessage"/> as a raw JSON body to a fixed exchange and routing key.
/// </summary>
internal sealed class RawJsonPublishRegistration<TMessage>(
    string exchangeName,
    string routingKey,
    string exchangeType) : RawJsonPublishRegistration
    where TMessage : class
{
    /// <summary>
    /// The content type MassTransit's raw JSON serializer is registered under.
    /// </summary>
    private static readonly ContentType RawJsonContentType = new("application/json");

    public override void Configure(IRabbitMqBusFactoryConfigurator cfg)
    {
        cfg.Message<TMessage>(m => m.SetEntityName(exchangeName));
        cfg.Publish<TMessage>(p => p.ExchangeType = exchangeType);
        cfg.Send<TMessage>(s => s.UseRoutingKeyFormatter(_ => routingKey));

        // Switch ONLY this message type to the raw JSON serializer; every other message keeps the
        // MassTransit envelope. Setting ContentType alone is NOT enough: the header changes but the
        // body is still enveloped (observed in RawJsonInteropTests), so the serializer is set explicitly.
        cfg.ConfigurePublish(pipe => pipe.UseExecute(ctx =>
        {
            if (ctx is not PublishContext<TMessage>)
                return;

            ctx.Serializer = ctx.Serialization.GetMessageSerializer(RawJsonContentType);
            ctx.ContentType = RawJsonContentType;
        }));
    }
}
