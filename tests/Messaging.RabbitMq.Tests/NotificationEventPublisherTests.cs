using MassTransit;
using Messaging.RabbitMq.Publishers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Contracts.Events;

namespace Messaging.RabbitMq.Tests;

/// <summary>
/// Unit tests for <see cref="NotificationEventPublisher"/>.
/// </summary>
public class NotificationEventPublisherTests
{
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly ILogger<NotificationEventPublisher> _logger;
    private readonly NotificationEventPublisher _publisher;

    public NotificationEventPublisherTests()
    {
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _logger = NullLogger<NotificationEventPublisher>.Instance;
        _publisher = new NotificationEventPublisher(_mockPublishEndpoint.Object, _logger);
    }

    [Fact]
    public async Task PublishAsync_ShouldPublishEventToRabbitMq()
    {
        // Arrange
        var evt = new QuestionnaireSubmittedEvent
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            QuestionnaireId = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
            TemplateName = "Test Template",
            RespondentName = "Test Respondent"
        };

        // Act
        await _publisher.PublishAsync(evt);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(evt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var evt = new TemplateUpdatedEvent
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TemplateId = Guid.NewGuid(),
            TemplateName = "Test Template",
            UpdatedByUserName = "Test User"
        };

        // Act
        var act = () => _publisher.PublishAsync(evt);

        // Assert - Should not throw
        await act.Should().NotThrowAsync();
        _mockPublishEndpoint.Verify(
            x => x.Publish(evt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenExceptionThrown_ShouldRethrow()
    {
        // Arrange
        var evt = new UserInvitedEvent
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TenantName = "Test Tenant",
            InvitedByUserName = "Test User",
            Role = "Admin"
        };

        var expectedException = new InvalidOperationException("Connection failed");
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<UserInvitedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = () => _publisher.PublishAsync(evt);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection failed");
    }

    [Fact]
    public async Task PublishAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var evt = new MenuUpdatedEvent
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MenuId = Guid.NewGuid(),
            MenuName = "Test Menu",
            UpdatedByUserName = "Test User"
        };

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _publisher.PublishAsync(evt, token);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(evt, token),
            Times.Once);
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPublishAllEvents()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var events = new[]
        {
            new SubscriptionExpiringEvent
            {
                TenantId = tenantId,
                UserId = Guid.NewGuid(),
                PlanName = "Plan 1",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                DaysUntilExpiry = 7
            },
            new SubscriptionExpiringEvent
            {
                TenantId = tenantId,
                UserId = Guid.NewGuid(),
                PlanName = "Plan 2",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
                DaysUntilExpiry = 14
            },
            new SubscriptionExpiringEvent
            {
                TenantId = tenantId,
                UserId = Guid.NewGuid(),
                PlanName = "Plan 3",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                DaysUntilExpiry = 30
            }
        };

        // Act
        await _publisher.PublishBatchAsync(events);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<SubscriptionExpiringEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task PublishBatchAsync_WithEmptyCollection_ShouldNotPublishAnything()
    {
        // Arrange
        var events = Array.Empty<PaymentDueEvent>();

        // Act
        await _publisher.PublishBatchAsync(events);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<PaymentDueEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishBatchAsync_WhenOneEventFails_ShouldStopAndRethrow()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var events = new[]
        {
            new PaymentDueEvent
            {
                TenantId = tenantId,
                UserId = Guid.NewGuid(),
                Amount = 100m,
                Currency = "USD",
                DueDate = DateTimeOffset.UtcNow.AddDays(3)
            },
            new PaymentDueEvent
            {
                TenantId = tenantId,
                UserId = Guid.NewGuid(),
                Amount = 200m,
                Currency = "EUR",
                DueDate = DateTimeOffset.UtcNow.AddDays(5)
            }
        };

        var callCount = 0;
        _mockPublishEndpoint
            .Setup(x => x.Publish(It.IsAny<PaymentDueEvent>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.CompletedTask;
                }
                throw new InvalidOperationException("Second publish failed");
            });

        // Act
        var act = () => _publisher.PublishBatchAsync(events);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Second publish failed");

        // First event was published, second failed
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<PaymentDueEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PublishBatchAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var events = new[]
        {
            new PaymentDueEvent
            {
                TenantId = tenantId,
                UserId = Guid.NewGuid(),
                Amount = 50m,
                Currency = "GBP",
                DueDate = DateTimeOffset.UtcNow.AddDays(10)
            }
        };

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _publisher.PublishBatchAsync(events, token);

        // Assert
        _mockPublishEndpoint.Verify(
            x => x.Publish(It.IsAny<PaymentDueEvent>(), token),
            Times.Once);
    }
}
