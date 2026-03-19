using MassTransit;
using Microsoft.Extensions.Logging;
using NotificationService.Contracts.Events;

namespace Messaging.RabbitMq.Publishers;

/// <summary>
/// Implementation of <see cref="INotificationEventPublisher"/> using MassTransit.
/// Provides both strict publishing (throws on failure) and graceful degradation
/// (fire-and-forget) modes for notification events.
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

    /// <inheritdoc />
    public async Task<bool> TryPublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent
    {
        try
        {
            await PublishAsync(@event, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            // Error is already logged in PublishAsync. Log the degradation decision.
            _logger.LogWarning(ex,
                "Notification publish failed for {EventType} (user {UserId}). " +
                "Graceful degradation: primary operation will continue without notification",
                @event.NotificationType,
                @event.UserId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> TryPublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent
    {
        var eventList = events.ToList();
        var successCount = 0;

        _logger.LogDebug(
            "Attempting to publish batch of {Count} notification events with graceful degradation",
            eventList.Count);

        foreach (var @event in eventList)
        {
            if (await TryPublishAsync(@event, cancellationToken))
                successCount++;
        }

        if (successCount < eventList.Count)
        {
            _logger.LogWarning(
                "Batch publish partially failed: {SuccessCount}/{TotalCount} events published successfully",
                successCount,
                eventList.Count);
        }

        return successCount;
    }
}
