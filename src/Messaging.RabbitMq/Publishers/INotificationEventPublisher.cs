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
    /// Throws on failure — use <see cref="TryPublishAsync{TEvent}"/> for fire-and-forget scenarios.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must implement INotificationEvent).</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent;

    /// <summary>
    /// Publishes multiple notification events to RabbitMQ.
    /// Useful for batch operations. Throws on failure.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must implement INotificationEvent).</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent;

    /// <summary>
    /// Attempts to publish a notification event to RabbitMQ with graceful degradation.
    /// If publishing fails (e.g., RabbitMQ is down), the error is logged but NOT thrown.
    /// Use this for fire-and-forget notifications where the primary operation must not fail.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must implement INotificationEvent).</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if published successfully; <c>false</c> if the publish failed.</returns>
    Task<bool> TryPublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent;

    /// <summary>
    /// Attempts to publish multiple notification events with graceful degradation.
    /// If publishing fails, the error is logged but NOT thrown.
    /// Returns the number of events successfully published.
    /// </summary>
    /// <typeparam name="TEvent">The event type (must implement INotificationEvent).</typeparam>
    /// <param name="events">The events to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of events successfully published.</returns>
    Task<int> TryPublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
        where TEvent : class, INotificationEvent;
}
