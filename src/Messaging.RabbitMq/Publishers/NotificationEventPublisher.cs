using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Contracts.Events;

namespace Messaging.RabbitMq.Publishers;

/// <summary>
/// Implementation of <see cref="INotificationEventPublisher"/> using MassTransit.
/// </summary>
internal sealed class NotificationEventPublisher : INotificationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<NotificationEventPublisher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationEventPublisher"/> class.
    /// </summary>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    /// <param name="logger">The logger.</param>
    public NotificationEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<NotificationEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent
    {
        _logger.LogDebug(
            "Publishing notification event {EventType} for user {UserId} in tenant {TenantId}",
            @event.NotificationType,
            @event.UserId,
            @event.TenantId);

        try
        {
            await _publishEndpoint.Publish(@event, cancellationToken);

            _logger.LogInformation(
                "Successfully published notification event {EventType} for user {UserId}",
                @event.NotificationType,
                @event.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish notification event {EventType} for user {UserId}",
                @event.NotificationType,
                @event.UserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent
    {
        var eventList = events.ToList();

        _logger.LogDebug("Publishing batch of {Count} notification events", eventList.Count);

        foreach (var @event in eventList)
        {
            await PublishAsync(@event, cancellationToken);
        }
    }
}
