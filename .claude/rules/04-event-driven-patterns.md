# Event-Driven Patterns — Kafka, Events & Sagas

## Event Envelope (CHUẨN BẮT BUỘC)

```csharp
// src/Shared/Contracts/IntegrationEvent.cs
public abstract record IntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid(); // idempotency key
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int SchemaVersion { get; init; } = 1;
    // correlationId được carry qua OpenTelemetry header
}
```

## Kafka Topic Naming Convention

```
Format: {service}.{entity}.{action}  (lowercase, dots)

order.order.created
order.order.status-updated
driver.driver.status-updated
driver.vehicle.assigned
shipment.shipment.created
shipment.shipment.status-updated
tracking.location.updated
payment.payment.completed
payment.payment.failed
userregistered          ← legacy format, giữ nguyên
```

**DLQ format:** `{topic}.dlq`

## Kafka Topic Reference

| Topic | Producer | Consumers |
|---|---|---|
| `userregistered` | Identity | Driver (tạo profile) |
| `order.order.created` | Order | Shipment (start saga) |
| `driver.driver.status-updated` | Driver | Shipment |
| `driver.vehicle.assigned` | Driver | Shipment |
| `shipment.shipment.status-updated` | Shipment | Order, Notification |
| `tracking.location.updated` | Tracking | Notification |
| `payment.payment.completed` | Payment | Order, Notification |

## Kafka Consumer Pattern (BẮT BUỘC)

```csharp
public sealed class OrderCreatedConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IMediator _mediator;
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IIdempotencyStore _idempotency;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("order.order.created");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);

                // 1. Extract OpenTelemetry trace context từ headers
                var parentContext = ExtractTraceContext(result.Message.Headers);
                using var activity = ActivitySources.Current.StartActivity(
                    "consume.order.order.created",
                    ActivityKind.Consumer,
                    parentContext);

                var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value)!;

                // 2. Idempotency check — BẮTBUỘC
                if (await _idempotency.HasProcessedAsync(@event.MessageId, stoppingToken))
                {
                    _logger.LogInformation("Duplicate event {MessageId}, skipping", @event.MessageId);
                    _consumer.Commit(result);
                    continue;
                }

                // 3. Process
                await _mediator.Send(new HandleOrderCreatedCommand(@event), stoppingToken);

                // 4. Mark processed
                await _idempotency.MarkProcessedAsync(@event.MessageId, stoppingToken);

                // 5. Commit offset AFTER successful processing
                _consumer.Commit(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing message, routing to DLQ");
                await RouteToDlqAsync(ex, stoppingToken);
                _consumer.Commit(result); // commit to avoid reprocessing loop
            }
        }
    }
}
```

## Kafka Producer Pattern

```csharp
public sealed class OrderCreatedProducer : IOrderCreatedProducer
{
    private readonly IProducer<string, string> _producer;
    private readonly ActivitySource _activitySource;

    public async Task PublishAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        using var activity = _activitySource.StartActivity("publish.order.order.created", ActivityKind.Producer);

        var headers = new Headers();
        // Inject traceparent vào Kafka header (OpenTelemetry W3C format)
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activity!.Context, Baggage.Current),
            headers,
            (h, key, value) => h.Add(key, Encoding.UTF8.GetBytes(value)));

        var message = new Message<string, string>
        {
            Key = @event.MessageId.ToString(),
            Value = JsonSerializer.Serialize(@event),
            Headers = headers
        };

        await _producer.ProduceAsync("order.order.created", message, ct);

        _logger.LogInformation("Published {EventType} MessageId={MessageId}",
            nameof(OrderCreatedEvent), @event.MessageId);
    }
}
```

## Idempotency Store (Redis)

```csharp
public sealed class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<bool> HasProcessedAsync(Guid messageId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"idempotency:{messageId}");
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"idempotency:{messageId}", "1", Ttl);
    }
}
```

## Choreography Saga Pattern (Shipment)

```
OrderCreated  →  Shipment Service creates ShipmentSagaState (MongoDB)
     ↓
[SYNC] Call Route Service → get distance/route
     ↓
[SYNC] Call Optimizer → get driver assignment
     ↓
Publish DriverAssignmentRequestedEvent
     ↓
Driver Service marks Driver=Busy, Vehicle=InUse
Publish DriverAssignedEvent
     ↓
Order Service updates Order.Status = AssignedToDriver
Publish ShipmentStartedEvent
     ↓
Tracking Service creates TrackingSession
```

**Compensating transactions (failure path):**
```
Optimizer fail → retry 3x → fallback greedy → if still fail →
  publish ShipmentFailedEvent →
  Order.UpdateStatus(Pending)  [compensate]
  Driver.UpdateStatus(Idle)    [compensate if already assigned]
```

## Saga State (MongoDB)

```csharp
public sealed class ShipmentSagaState
{
    [BsonId] public Guid SagaId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public Guid? AssignedVehicleId { get; set; }
    public ShipmentSagaStatus Status { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int _version { get; set; } // optimistic concurrency
}
```

## Event Naming Rules

- **Past tense**: `OrderCreated`, `DriverAssigned`, `PaymentCompleted`
- **Never imperative**: `CreateOrder`, `AssignDriver` (these are commands, not events)
- **Class name**: `{Context}{EntityOrAction}Event`
- **Topic**: `{service}.{entity}.{action}` lowercase

## Partition Key Strategy

| Topic | Partition Key | Reason |
|---|---|---|
| `order.order.created` | `orderId` | Order ordering |
| `shipment.*` | `shipmentId` | Saga consistency |
| `driver.*` | `driverId` | Driver state ordering |
| `tracking.location.updated` | `driverId` | GPS ordering per driver |
| `payment.*` | `orderId` | Payment ordering |
