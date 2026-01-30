namespace Messaging.RabbitMq.Configuration;

/// <summary>
/// Configuration options for RabbitMQ connection.
/// </summary>
public sealed class RabbitMqConfiguration
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "RabbitMq";

    /// <summary>
    /// RabbitMQ host (e.g., "localhost" or "rabbitmq").
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ port (default: 5672).
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Virtual host (default: "/").
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public string Username { get; set; } = "guest";

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// Gets the connection string in AMQP URI format.
    /// </summary>
    public string ConnectionString =>
        $"amqp://{Username}:{Password}@{Host}:{Port}{VirtualHost}";
}
