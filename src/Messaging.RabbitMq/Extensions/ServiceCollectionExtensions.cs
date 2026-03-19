using MassTransit;
using Messaging.RabbitMq.Configuration;
using Messaging.RabbitMq.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Messaging.RabbitMq.Extensions;

/// <summary>
/// Extension methods for configuring RabbitMQ messaging in the service collection.
/// Provides resilience patterns including retry, circuit breaker, and error queues.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds RabbitMQ messaging with MassTransit including resilience patterns.
    /// Configures retry policies, circuit breakers, and error/dead-letter queues.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing RabbitMq section.</param>
    /// <param name="configureConsumers">Optional action to configure consumers.</param>
    /// <param name="resilienceOptions">Optional resilience options. Uses production defaults if not specified.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        ResilienceOptions? resilienceOptions = null)
    {
        var rabbitConfig = configuration
            .GetSection(RabbitMqConfiguration.SectionName)
            .Get<RabbitMqConfiguration>() ?? new RabbitMqConfiguration();

        var resilience = resilienceOptions ?? ResilienceOptions.Default;

        services.AddMassTransit(x =>
        {
            // Allow services to register their own consumers
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig.Host, (ushort)rabbitConfig.Port, rabbitConfig.VirtualHost, h =>
                {
                    h.Username(rabbitConfig.Username);
                    h.Password(rabbitConfig.Password);
                });

                ConfigureResilience(context, cfg, resilience);
            });

            ConfigureHealthChecks(x);
        });

        // Register the notification event publisher with graceful degradation
        services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();

        return services;
    }

    /// <summary>
    /// Adds RabbitMQ messaging using a connection string with resilience patterns.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The RabbitMQ connection string in AMQP format.</param>
    /// <param name="configureConsumers">Optional action to configure consumers.</param>
    /// <param name="resilienceOptions">Optional resilience options. Uses production defaults if not specified.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        string connectionString,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        ResilienceOptions? resilienceOptions = null)
    {
        var resilience = resilienceOptions ?? ResilienceOptions.Default;

        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(connectionString));

                ConfigureResilience(context, cfg, resilience);
            });

            ConfigureHealthChecks(x);
        });

        services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();

        return services;
    }

    /// <summary>
    /// Configures all resilience patterns on the RabbitMQ bus: retry, circuit breaker,
    /// in-memory outbox, and consumer endpoints with error queues.
    /// </summary>
    internal static void ConfigureResilience(
        IBusRegistrationContext context,
        IRabbitMqBusFactoryConfigurator cfg,
        ResilienceOptions resilience)
    {
        // 1. Message retry with configurable intervals (exponential backoff).
        //    Failed messages are retried in-process before being moved to the error queue.
        cfg.UseMessageRetry(r => r.Intervals(resilience.RetryIntervals));

        // 2. Circuit breaker: trips after repeated failures, pausing message consumption
        //    to let the system recover. Messages queue in RabbitMQ during the open period.
        if (resilience.EnableCircuitBreaker)
        {
            cfg.UseCircuitBreaker(cb =>
            {
                cb.TripThreshold = resilience.CircuitBreakerTripThreshold;
                cb.TrackingPeriod = resilience.CircuitBreakerTrackingPeriod;
                cb.ActiveThreshold = resilience.CircuitBreakerTripThreshold;
                cb.ResetInterval = resilience.CircuitBreakerActiveDuration;
            });
        }

        // 3. In-memory outbox: ensures messages published during consumer execution
        //    are only sent after the consumer completes successfully.
        if (resilience.UseInMemoryOutbox)
            cfg.UseInMemoryOutbox(context);

        // 4. Configure consumer endpoints. MassTransit automatically creates
        //    _error and _skipped queues for each consumer endpoint.
        //    Messages that fail all retries are moved to the _error queue (dead-letter).
        cfg.ConfigureEndpoints(context);
    }

    /// <summary>
    /// Configures health checks to report Degraded instead of Unhealthy when RabbitMQ is down.
    /// This prevents readiness probe failures for publisher-only services where RabbitMQ
    /// is not critical for serving HTTP requests (notifications will queue and retry).
    /// </summary>
    internal static void ConfigureHealthChecks(IBusRegistrationConfigurator x)
    {
        x.ConfigureHealthCheckOptions(options =>
        {
            options.MinimalFailureStatus = HealthStatus.Degraded;
            options.Tags.Add("messaging");
        });
    }
}
