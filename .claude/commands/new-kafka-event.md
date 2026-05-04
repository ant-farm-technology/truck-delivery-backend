# /new-kafka-event — Scaffold Kafka Event, Producer & Consumer

Scaffold đầy đủ một Kafka event với producer và consumer cho cross-service communication.

**Event name:** $ARGUMENTS

## Yêu cầu

Tạo các files sau (event name = `$ARGUMENTS`):

### 1. Event Contract (shared/Contracts project)
```
src/Shared/Contracts/Events/$ARGUMENTSEvent.cs
```
```csharp
public sealed record $ARGUMENTSEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid(); // idempotency key
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int SchemaVersion { get; init; } = 1;
    // ... domain-specific properties
}
```

### 2. Producer
```
Infrastructure/Messaging/Kafka/Producers/$ARGUMENTSProducer.cs
```
Pattern bắt buộc:
```csharp
public sealed class $ARGUMENTSProducer : I$ARGUMENTSProducer
{
    // Inject: IProducer<string, string>, ILogger, ActivitySource
    
    public async Task PublishAsync($ARGUMENTSEvent @event, CancellationToken ct)
    {
        // 1. Tạo Activity (OpenTelemetry span)
        // 2. Inject traceparent vào Kafka Message Headers
        // 3. Serialize event to JSON
        // 4. Produce với key = MessageId
        // 5. Log với structured logging
    }
}
```

### 3. Consumer
```
Infrastructure/Messaging/Kafka/Consumers/$ARGUMENTSConsumer.cs
```
Pattern bắt buộc:
```csharp
public sealed class $ARGUMENTSConsumer : BackgroundService
{
    // Inject: IConsumer<string, string>, IMediator, ILogger, I{Entity}IdempotencyStore
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(Topics.$ARGUMENTS);
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Consume message
            // 2. Extract traceparent từ Headers → restore Activity context
            // 3. Idempotency check: MessageId đã xử lý chưa?
            // 4. Deserialize → dispatch qua IMediator
            // 5. Commit offset sau khi xử lý thành công
            // 6. On exception: route to Dead Letter Queue topic
        }
    }
}
```

### 4. Dead Letter Queue Consumer (stub)
```
Infrastructure/Messaging/Kafka/Consumers/$ARGUMENTSDlqConsumer.cs
```
- Log và alert khi message vào DLQ
- Retry policy (3 lần) trước khi vào DLQ

### 5. Idempotency Store Interface
```
Application/Interfaces/I$ARGUMENTSIdempotencyStore.cs
```
- `Task<bool> HasProcessedAsync(Guid messageId, CancellationToken ct)`
- `Task MarkProcessedAsync(Guid messageId, CancellationToken ct)`
- Implementation dùng Redis với TTL 24h

## Kafka Topic naming
- Topic name: lowercase, dots: `{service}.{entity}.{action}`
- DLQ topic: `{original-topic}.dlq`

## Rules
- Mọi consumer PHẢI có idempotency check
- Mọi message PHẢI carry OpenTelemetry trace context qua Kafka headers
- Producer PHẢI log MessageId ở structured log
- Consumer offset PHẢI commit sau khi xử lý xong, không auto-commit
- SchemaVersion field để backward compatibility sau này
