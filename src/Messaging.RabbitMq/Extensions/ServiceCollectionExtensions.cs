using MassTransit;
using Messaging.RabbitMq.Configuration;
using Messaging.RabbitMq.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Messaging.RabbitMq.Extensions;

/// <summary>
/// Extension methods for configuring RabbitMQ messaging in the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    private const int RetryIntervalSeconds1 = 1;
    private const int RetryIntervalSeconds2 = 5;
    private const int RetryIntervalSeconds3 = 15;
    private const int RetryIntervalSeconds4 = 30;

    /// <summary>
    /// Adds RabbitMQ messaging with MassTransit for publishing notification events.
    /// This must be called by all services that need to publish notifications.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing RabbitMq section.</param>
    /// <param name="configureConsumers">Optional action to configure consumers (for Notification Service only).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var rabbitConfig = configuration
            .GetSection(RabbitMqConfiguration.SectionName)
            .Get<RabbitMqConfiguration>() ?? new RabbitMqConfiguration();

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

                // Configure message retry
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(RetryIntervalSeconds1),
                    TimeSpan.FromSeconds(RetryIntervalSeconds2),
                    TimeSpan.FromSeconds(RetryIntervalSeconds3),
                    TimeSpan.FromSeconds(RetryIntervalSeconds4)
                ));

                // Configure error handling
                cfg.UseInMemoryOutbox(context);

                // Configure endpoints for consumers
                cfg.ConfigureEndpoints(context);
            });

            // Configure health checks to report Degraded instead of Unhealthy when RabbitMQ is down.
            // This prevents readiness probe failures for publisher-only services where RabbitMQ
            // is not critical for serving requests (notifications will queue and retry).
            x.ConfigureHealthCheckOptions(options =>
            {
                options.MinimalFailureStatus = HealthStatus.Degraded;
                options.Tags.Add("messaging");
            });
        });

        // Register the notification event publisher
        services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();

        return services;
    }

    /// <summary>
    /// Simplified overload using connection string from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The RabbitMQ connection string in AMQP format.</param>
    /// <param name="configureConsumers">Optional action to configure consumers.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        string connectionString,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(connectionString));

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(RetryIntervalSeconds1),
                    TimeSpan.FromSeconds(RetryIntervalSeconds2),
                    TimeSpan.FromSeconds(RetryIntervalSeconds3)
                ));

                cfg.ConfigureEndpoints(context);
            });

            // Configure health checks to report Degraded instead of Unhealthy
            x.ConfigureHealthCheckOptions(options =>
            {
                options.MinimalFailureStatus = HealthStatus.Degraded;
                options.Tags.Add("messaging");
            });
        });

        services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();

        return services;
    }
}
