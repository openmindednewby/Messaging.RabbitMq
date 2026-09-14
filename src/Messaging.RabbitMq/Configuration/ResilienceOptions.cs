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
    /// Exception types (and their subclasses) that are never retried: the message moves straight to the
    /// endpoint's <c>_error</c> queue on the first failure. Applies bus-wide, so use exception types
    /// specific to the failure (e.g. an unknown-tenant or contract-violation exception). Defaults to empty.
    /// </summary>
    public Type[] NonRetryableExceptions { get; set; } = [];

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
    /// Whether to defer publishes made during a consumer until that consumer completes
    /// successfully. Defaults to <c>true</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>This is NOT a transactional outbox, and this documentation used to say it was.</b>
    /// MassTransit's in-memory outbox holds pending publishes in <b>process memory</b> and releases
    /// them when the consumer returns without throwing. It is not tied to any database transaction
    /// and it does not survive a crash: if the process dies between the consumer's
    /// <c>SaveChanges</c> and the release, the messages are <b>gone</b>, with no record that they
    /// ever existed.
    /// </para>
    /// <para>
    /// The previous wording — "the in-memory outbox for transactional consistency" — would lead a
    /// reader to believe durability exists here where it does not, which is the most expensive kind
    /// of documentation error: it stops people building the thing they actually need.
    /// </para>
    /// <para>
    /// <b>What it IS good for:</b> not publishing an event for work that then threw. That is
    /// genuinely useful and worth leaving on by default.
    /// </para>
    /// <para>
    /// <b>If you need a real transactional outbox</b> — the message durably committed in the SAME
    /// database transaction as the state change — you need either MassTransit's
    /// <c>AddEntityFrameworkOutbox</c> + <c>UseBusOutbox</c> (not wired up by this package), or a
    /// domain outbox table. Zygos F-07 implements the latter, because its failure destination is a
    /// reconciliation break rather than an error queue.
    /// </para>
    /// </remarks>
    public bool UseInMemoryOutbox { get; set; } = true;

    /// <summary>
    /// Gets the default resilience options with standard production-ready settings.
    /// </summary>
    public static ResilienceOptions Default => new();
}
