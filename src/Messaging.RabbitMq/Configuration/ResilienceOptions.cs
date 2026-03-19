namespace Messaging.RabbitMq.Configuration;

/// <summary>
/// Configuration options for MassTransit resilience patterns including
/// retry policies, circuit breakers, and rate limiting.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// Default retry intervals used when no custom intervals are specified.
    /// Exponential backoff: 1s, 5s, 15s, 30s.
    /// </summary>
    private static readonly TimeSpan[] DefaultRetryIntervals =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30)
    ];

    /// <summary>
    /// Default number of consecutive failures before the circuit breaker trips.
    /// </summary>
    private const int DefaultCircuitBreakerTripThreshold = 5;

    /// <summary>
    /// Default tracking period in seconds for the circuit breaker.
    /// </summary>
    private const int DefaultCircuitBreakerTrackingPeriodSeconds = 30;

    /// <summary>
    /// Default duration in seconds the circuit breaker stays open before resetting.
    /// </summary>
    private const int DefaultCircuitBreakerActiveDurationSeconds = 60;

    /// <summary>
    /// Retry intervals for message consumption failures.
    /// Defaults to exponential backoff: 1s, 5s, 15s, 30s.
    /// </summary>
    public TimeSpan[] RetryIntervals { get; set; } = DefaultRetryIntervals;

    /// <summary>
    /// Whether to enable the circuit breaker on consumer endpoints.
    /// When enabled, consumers will stop processing messages after repeated failures,
    /// allowing the system to recover before resuming.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures within <see cref="CircuitBreakerTrackingPeriod"/>
    /// before the circuit breaker trips. Defaults to 5.
    /// </summary>
    public int CircuitBreakerTripThreshold { get; set; } = DefaultCircuitBreakerTripThreshold;

    /// <summary>
    /// The time window over which failures are tracked for the circuit breaker.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerTrackingPeriod { get; set; } =
        TimeSpan.FromSeconds(DefaultCircuitBreakerTrackingPeriodSeconds);

    /// <summary>
    /// How long the circuit breaker stays open (rejecting messages) before
    /// attempting to allow messages through again. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerActiveDuration { get; set; } =
        TimeSpan.FromSeconds(DefaultCircuitBreakerActiveDurationSeconds);

    /// <summary>
    /// Whether to use the in-memory outbox for transactional consistency.
    /// When enabled, messages are not sent until the consumer completes successfully.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool UseInMemoryOutbox { get; set; } = true;

    /// <summary>
    /// Gets the default resilience options with standard production-ready settings.
    /// </summary>
    public static ResilienceOptions Default => new();
}
