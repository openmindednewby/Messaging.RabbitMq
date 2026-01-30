using Messaging.RabbitMq.Configuration;

namespace Messaging.RabbitMq.Tests;

/// <summary>
/// Unit tests for <see cref="RabbitMqConfiguration"/>.
/// </summary>
public class RabbitMqConfigurationTests
{
    [Fact]
    public void DefaultValues_ShouldBeSetCorrectly()
    {
        // Arrange & Act
        var config = new RabbitMqConfiguration();

        // Assert
        config.Host.Should().Be("localhost");
        config.Port.Should().Be(5672);
        config.VirtualHost.Should().Be("/");
        config.Username.Should().Be("guest");
        config.Password.Should().Be("guest");
    }

    [Fact]
    public void ConnectionString_ShouldBeConstructedCorrectly()
    {
        // Arrange
        var config = new RabbitMqConfiguration
        {
            Host = "rabbitmq.example.com",
            Port = 5673,
            VirtualHost = "/myapp",
            Username = "myuser",
            Password = "mypassword"
        };

        // Act
        var connectionString = config.ConnectionString;

        // Assert
        connectionString.Should().Be("amqp://myuser:mypassword@rabbitmq.example.com:5673/myapp");
    }

    [Fact]
    public void ConnectionString_WithDefaultValues_ShouldBeCorrect()
    {
        // Arrange
        var config = new RabbitMqConfiguration();

        // Act
        var connectionString = config.ConnectionString;

        // Assert
        connectionString.Should().Be("amqp://guest:guest@localhost:5672/");
    }

    [Fact]
    public void SectionName_ShouldBeRabbitMq()
    {
        // Assert
        RabbitMqConfiguration.SectionName.Should().Be("RabbitMq");
    }
}
