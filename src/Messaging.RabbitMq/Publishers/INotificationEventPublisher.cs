using NotificationService.Contracts.Events;

namespace Messaging.RabbitMq.Publishers;

/// <summary>
/// Interface for publishing notification events to RabbitMQ.
/// Inject this into your services to send notifications.
/// </summary>
public interface INotificationEventPublisher
{
    /// <summary>
    /// Publishes a notification event to RabbitMQ.
    /// The Notification Service will consume this event and deliver to the user.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must implement INotificationEvent).</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent;

    /// <summary>
    /// Publishes multiple notification events to RabbitMQ.
    /// Useful for batch operations.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must implement INotificationEvent).</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent;
}
