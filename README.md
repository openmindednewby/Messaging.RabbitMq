# Messaging.RabbitMq

Shared RabbitMQ configuration and publisher utilities for all services. **REQUIRED** for publishing notification events.

## Installation

```bash
dotnet add package Messaging.RabbitMq
```

## Configuration

Add to your `appsettings.json`:

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  }
}
```

## Usage

### Register in Program.cs

```csharp
using Messaging.RabbitMq.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add RabbitMQ messaging (required for all services that publish notifications)
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// For services that also consume events (e.g., NotificationService):
builder.Services.AddRabbitMqMessaging(builder.Configuration, x =>
{
    x.AddConsumer<NotificationEventConsumer>();
});
```

### Custom Resilience Options

Override the default resilience settings per service:

```csharp
using Messaging.RabbitMq.Configuration;

builder.Services.AddRabbitMqMessaging(builder.Configuration,
    configureConsumers: x => { x.AddConsumer<MyConsumer>(); },
    resilienceOptions: new ResilienceOptions
    {
        RetryIntervals = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)],
        CircuitBreakerTripThreshold = 3,
        CircuitBreakerActiveDuration = TimeSpan.FromSeconds(30),
    });
```

### Publish Events

Inject `INotificationEventPublisher` and publish events:

```csharp
using Messaging.RabbitMq.Publishers;
using NotificationService.Contracts.Events;

public class QuestionnaireService
{
    private readonly INotificationEventPublisher _notificationPublisher;

    public QuestionnaireService(INotificationEventPublisher notificationPublisher)
    {
        _notificationPublisher = notificationPublisher;
    }

    public async Task SubmitQuestionnaireAsync(QuestionnaireSubmission submission)
    {
        // ... save submission ...

        // Publish notification event
        await _notificationPublisher.PublishAsync(new QuestionnaireSubmittedEvent
        {
            TenantId = submission.TenantId,
            UserId = templateOwnerId,
            QuestionnaireId = submission.Id,
            TemplateId = submission.TemplateId,
            TemplateName = template.Name,
            RespondentName = submission.RespondentName
        });
    }
}
```

### Graceful Degradation (Fire-and-Forget)

Use `TryPublishAsync` when notifications are non-critical and the primary operation
must succeed even if RabbitMQ is down:

```csharp
public async Task UpdateMenuAsync(Menu menu)
{
    // Primary operation: save menu (must succeed)
    await _repository.UpdateAsync(menu);

    // Non-critical: send notification (must NOT fail the menu update)
    await _notificationPublisher.TryPublishAsync(new MenuUpdatedEvent
    {
        TenantId = menu.TenantId,
        UserId = menu.UserId,
        MenuId = menu.Id,
        MenuName = menu.Name,
        UpdatedByUserName = currentUser.Name
    });
    // Returns false if publish failed -- error is logged, no exception thrown
}
```

### Batch Publishing

For multiple notifications:

```csharp
var events = users.Select(u => new TemplateUpdatedEvent
{
    TenantId = tenantId,
    UserId = u.Id,
    TemplateId = template.Id,
    TemplateName = template.Name,
    UpdatedByUserName = currentUser.Name
});

await _notificationPublisher.PublishBatchAsync(events);

// Or with graceful degradation:
int successCount = await _notificationPublisher.TryPublishBatchAsync(events);
```

## Resilience Features

- **Automatic Retry**: Messages are retried with exponential backoff (1s, 5s, 15s, 30s)
- **Circuit Breaker**: Trips after 5 failures in 30s, resets after 60s. Prevents cascading failures by pausing message consumption while the system recovers.
- **In-Memory Outbox**: Publishes made during a consumer are held until it completes successfully, so a consumer that throws does not announce work it did not do. ⚠️ **This is NOT a transactional outbox** — the pending messages live in *process memory*, are tied to no database transaction, and are lost on crash. For durability (message committed in the SAME transaction as the state change) you need MassTransit's `AddEntityFrameworkOutbox` + `UseBusOutbox`, which this package does not wire up, or a domain outbox table.
- **Error Queues**: MassTransit automatically creates `_error` and `_skipped` queues for each consumer endpoint. Messages that fail all retries are moved to `_error` (dead-letter).
- **Graceful Degradation**: `TryPublishAsync` / `TryPublishBatchAsync` swallow exceptions for fire-and-forget scenarios.
- **Health Check Degradation**: RabbitMQ health reports `Degraded` (not `Unhealthy`) so publisher-only services keep serving HTTP requests.
- **Structured Logging**: All publish operations are logged with correlation IDs

## Default Resilience Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Retry intervals | 1s, 5s, 15s, 30s | Exponential backoff |
| Circuit breaker enabled | true | Trips on repeated failures |
| Trip threshold | 5 failures | In tracking period |
| Tracking period | 30 seconds | Failure counting window |
| Reset interval | 60 seconds | Time before circuit resets |
| In-memory outbox | true | Defer publishes until the consumer succeeds (in-memory; **not** durable) |

## Dependencies

- `MassTransit.RabbitMQ` - Message broker abstraction
- `NotificationService.Contracts` - Event contracts

## License

MIT
