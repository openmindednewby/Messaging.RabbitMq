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
```

## Features

- **Automatic Retry**: Messages are retried with exponential backoff (1s, 5s, 15s, 30s)
- **In-Memory Outbox**: Ensures messages are published reliably within a transaction
- **Structured Logging**: All publish operations are logged with correlation IDs

## Dependencies

- `MassTransit.RabbitMQ` - Message broker abstraction
- `NotificationService.Contracts` - Event contracts

## License

MIT
