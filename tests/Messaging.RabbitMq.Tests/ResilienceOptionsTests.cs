using Messaging.RabbitMq.Configuration;

namespace Messaging.RabbitMq.Tests;

/// <summary>
/// Unit tests for <see cref="ResilienceOptions"/>.
/// </summary>
public class ResilienceOptionsTests
{
    [Fact]
    public void Default_ShouldHaveExpectedRetryIntervals()
    {
        // Arrange & Act
        var options = ResilienceOptions.Default;

        // Assert
        options.RetryIntervals.Should().HaveCount(4);
        options.RetryIntervals[0].Should().Be(TimeSpan.FromSeconds(1));
        options.RetryIntervals[1].Should().Be(TimeSpan.FromSeconds(5));
        options.RetryIntervals[2].Should().Be(TimeSpan.FromSeconds(15));
        options.RetryIntervals[3].Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Default_ShouldEnableCircuitBreaker()
    {
        // Arrange & Act
        var options = ResilienceOptions.Default;

        // Assert
        options.EnableCircuitBreaker.Should().BeTrue();
    }

    [Fact]
    public void Default_CircuitBreakerTripThreshold_ShouldBeFive()
    {
        // Arrange & Act
        var options = ResilienceOptions.Default;

        // Assert
        options.CircuitBreakerTripThreshold.Should().Be(5);
    }

    [Fact]
    public void Default_CircuitBreakerTrackingPeriod_ShouldBeThirtySeconds()
    {
        // Arrange & Act
        var options = ResilienceOptions.Default;

        // Assert
        options.CircuitBreakerTrackingPeriod.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Default_CircuitBreakerActiveDuration_ShouldBeSixtySeconds()
    {
        // Arrange & Act
        var options = ResilienceOptions.Default;

        // Assert
        options.CircuitBreakerActiveDuration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void Default_ShouldEnableInMemoryOutbox()
    {
        // Arrange & Act
        var options = ResilienceOptions.Default;

        // Assert
        options.UseInMemoryOutbox.Should().BeTrue();
    }

    [Fact]
    public void CustomOptions_ShouldOverrideDefaults()
    {
        // Arrange
        var customIntervals = new[]
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        };

        // Act
        var options = new ResilienceOptions
        {
            RetryIntervals = customIntervals,
            EnableCircuitBreaker = false,
            CircuitBreakerTripThreshold = 10,
            CircuitBreakerTrackingPeriod = TimeSpan.FromSeconds(60),
            CircuitBreakerActiveDuration = TimeSpan.FromSeconds(120),
            UseInMemoryOutbox = false
        };

        // Assert
        options.RetryIntervals.Should().BeEquivalentTo(customIntervals);
        options.EnableCircuitBreaker.Should().BeFalse();
        options.CircuitBreakerTripThreshold.Should().Be(10);
        options.CircuitBreakerTrackingPeriod.Should().Be(TimeSpan.FromSeconds(60));
        options.CircuitBreakerActiveDuration.Should().Be(TimeSpan.FromSeconds(120));
        options.UseInMemoryOutbox.Should().BeFalse();
    }

    [Fact]
    public void NewInstance_ShouldHaveSameDefaultsAsStaticDefault()
    {
        // Arrange & Act
        var instance = new ResilienceOptions();
        var staticDefault = ResilienceOptions.Default;

        // Assert
        instance.RetryIntervals.Should().BeEquivalentTo(staticDefault.RetryIntervals);
        instance.EnableCircuitBreaker.Should().Be(staticDefault.EnableCircuitBreaker);
        instance.CircuitBreakerTripThreshold.Should().Be(staticDefault.CircuitBreakerTripThreshold);
        instance.CircuitBreakerTrackingPeriod.Should().Be(staticDefault.CircuitBreakerTrackingPeriod);
        instance.CircuitBreakerActiveDuration.Should().Be(staticDefault.CircuitBreakerActiveDuration);
        instance.UseInMemoryOutbox.Should().Be(staticDefault.UseInMemoryOutbox);
    }
}
