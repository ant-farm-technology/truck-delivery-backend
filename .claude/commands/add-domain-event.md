# /add-domain-event — Add Domain Event with Full Kafka Propagation

Scaffold hoàn chỉnh một domain event từ Domain layer đến Kafka integration event.

**Arguments:** `$ARGUMENTS` = `{ServiceName} {EventName}` (ví dụ: `Order OrderDelivered`)

## Yêu cầu

### 1. Domain/Events/{EventName}DomainEvent.cs

```csharp
// Internal domain event — không expose ra ngoài service
public sealed record {EventName}DomainEvent(
    Guid {MainEntityId},
    // ... relevant data
) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
```

### 2. Shared/Contracts/Events/{EventName}Event.cs

```csharp
// Integration event — exposed qua Kafka, shared với consumers
public sealed record {EventName}Event : IntegrationEvent
{
    // IntegrationEvent base: MessageId (Guid), OccurredAt, SchemaVersion
    public required Guid {MainEntityId} { get; init; }
    // ... relevant payload (không expose domain entity trực tiếp)
}
```

### 3. Application/EventHandlers/{EventName}DomainEventHandler.cs

```csharp
// Domain event → Integration event (Outbox)
public sealed class {EventName}DomainEventHandler : INotificationHandler<{EventName}DomainEvent>
{
    private readonly IOutboxMessageRepository _outbox;
    private readonly ILogger<{EventName}DomainEventHandler> _logger;

    public async Task Handle({EventName}DomainEvent notification, CancellationToken ct)
    {
        var integrationEvent = new {EventName}Event
        {
            {MainEntityId} = notification.{MainEntityId},
            // map domain data → integration event
        };

        var outboxMessage = OutboxMessage.Create(
            eventType: nameof({EventName}Event),
            payload: JsonSerializer.Serialize(integrationEvent),
            topic: "{service}.{entity}.{action}"  // ví dụ: order.order.delivered
        );

        await _outbox.AddAsync(outboxMessage, ct);

        _logger.LogInformation("Queued {EventName}Event for MessageId={MessageId}",
            nameof({EventName}Event), integrationEvent.MessageId);
    }
}
```

### 4. Infrastructure/Messaging/Kafka/Producers/{EventName}Producer.cs

```csharp
public sealed class {EventName}Producer : I{EventName}Producer
{
    private readonly IProducer<string, string> _producer;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<{EventName}Producer> _logger;

    public async Task PublishAsync({EventName}Event @event, CancellationToken ct)
    {
        using var activity = _activitySource.StartActivity(
            $"kafka.publish.{topic}", ActivityKind.Producer);

        var headers = new Headers();
        InjectTraceContext(activity, headers);

        var message = new Message<string, string>
        {
            Key = @event.{MainEntityId}.ToString(),
            Value = JsonSerializer.Serialize(@event),
            Headers = headers
        };

        await _producer.ProduceAsync("{service}.{entity}.{action}", message, ct);

        _logger.LogInformation("Published {EventType} MessageId={MessageId} {EntityId}={Id}",
            nameof({EventName}Event), @event.MessageId, "{MainEntityId}", @event.{MainEntityId});
    }

    private static void InjectTraceContext(Activity? activity, Headers headers)
    {
        if (activity is null) return;
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            headers,
            (h, key, value) => h.Add(key, Encoding.UTF8.GetBytes(value)));
    }
}
```

### 5. Infrastructure/Messaging/Kafka/Consumers/{EventName}Consumer.cs (Consumer service bên nhận)

```csharp
// Tạo trong SERVICE TIÊU THỤ event này, không phải service phát
public sealed class {EventName}Consumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IMediator _mediator;
    private readonly IIdempotencyStore _idempotency;
    private readonly ILogger<{EventName}Consumer> _logger;
    private readonly IProducer<string, string> _dlqProducer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("{service}.{entity}.{action}");

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = _consumer.Consume(stoppingToken);

                var parentContext = ExtractTraceContext(result.Message.Headers);
                using var activity = ActivitySources.Current.StartActivity(
                    "kafka.consume.{EventName}",
                    ActivityKind.Consumer,
                    parentContext);

                var @event = JsonSerializer.Deserialize<{EventName}Event>(result.Message.Value)
                    ?? throw new InvalidOperationException("Failed to deserialize {EventName}Event");

                // Idempotency check
                if (await _idempotency.HasProcessedAsync(@event.MessageId, stoppingToken))
                {
                    _logger.LogDebug("Duplicate {EventName}Event MessageId={MessageId}, skipping",
                        nameof({EventName}Event), @event.MessageId);
                    _consumer.Commit(result);
                    continue;
                }

                await _mediator.Send(new Handle{EventName}Command(@event), stoppingToken);
                await _idempotency.MarkProcessedAsync(@event.MessageId, stoppingToken);
                _consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing {EventName}Event, routing to DLQ");
                if (result is not null)
                    await RouteToDlqAsync(result, ex, stoppingToken);
            }
        }
    }

    private static PropagationContext ExtractTraceContext(Headers headers)
    {
        var carrier = headers.ToDictionary(
            h => h.Key,
            h => Encoding.UTF8.GetString(h.GetValueBytes()));
        return Propagators.DefaultTextMapPropagator.Extract(default, carrier,
            (c, key) => c.TryGetValue(key, out var val) ? [val] : []);
    }

    private async Task RouteToDlqAsync(ConsumeResult<string, string> result, Exception ex, CancellationToken ct)
    {
        var dlqMessage = new Message<string, string>
        {
            Key = result.Message.Key,
            Value = result.Message.Value,
            Headers = result.Message.Headers
        };
        dlqMessage.Headers.Add("x-error-message", Encoding.UTF8.GetBytes(ex.Message));
        dlqMessage.Headers.Add("x-original-topic", Encoding.UTF8.GetBytes(result.Topic));
        await _dlqProducer.ProduceAsync("{service}.{entity}.{action}.dlq", dlqMessage, ct);
        _consumer.Commit(result);
    }
}
```

### 6. Application/Commands/Handle{EventName}Command.cs

```csharp
public sealed record Handle{EventName}Command({EventName}Event Event) : IRequest;

public sealed class Handle{EventName}CommandHandler : IRequestHandler<Handle{EventName}Command>
{
    public async Task Handle(Handle{EventName}Command request, CancellationToken ct)
    {
        var @event = request.Event;
        // Business logic responding to this event
        // Load aggregate, call domain method, save
    }
}
```

## Checklist

Sau khi tạo, kiểm tra:
- [ ] DomainEvent được `AddDomainEvent()` trong aggregate
- [ ] DomainEventHandler đăng ký trong DI container
- [ ] IntegrationEvent inherits `IntegrationEvent` base (có `MessageId`)
- [ ] Producer inject traceparent vào Kafka headers
- [ ] Consumer có idempotency check
- [ ] Consumer có DLQ handler
- [ ] Topic name theo format `{service}.{entity}.{action}`
- [ ] Outbox pattern (không publish Kafka trực tiếp từ handler)
