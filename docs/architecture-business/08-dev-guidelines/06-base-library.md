## 1. 🎯 Mục tiêu

Tạo một **shared library** dùng chung cho tất cả services:

- Chuẩn hoá event publishing / consuming
- Tích hợp Outbox pattern
- Idempotency handling
- Logging + tracing (OpenTelemetry)
- Retry / resilience

---

## 2. 🧠 Nguyên tắc thiết kế

---

### 2.1 Thin but powerful

Không over-engineer — chỉ abstract phần lặp lại

---

---

### 2.2 Convention over configuration

Default chuẩn → override khi cần

---

---

### 2.3 Pluggable

Cho phép thay Kafka / Redis / DB

---

---

### 2.4 Không leak domain

Infra lib không chứa business logic

---

---

## 3. 🧱 Modules trong Base Library

---

/Shared.Infrastructure  
 ├── EventBus  
 ├── Outbox  
 ├── Idempotency  
 ├── Kafka  
 ├── Observability  
 ├── Resilience  
 └── Common

---

---

## 4. 📡 EventBus Module

---

### Interface
```
public interface IEventBus
{
    Task PublishAsync<T>(T @event);
}
```

---

### Implementation (Kafka-backed)

```
public class KafkaEventBus : IEventBus
{
    private readonly IKafkaProducer _producer;

    public async Task PublishAsync<T>(T @event)
    {
        var envelope = EventEnvelope.Create(@event);

        await _producer.PublishAsync(
            topic: TopicResolver.Resolve(@event),
            key: envelope.AggregateId,
            value: envelope
        );
    }
}
```

---

## 5. 🧾 Event Envelope Builder

```
public class EventEnvelope
{
    public string EventId { get; set; }
    public string EventType { get; set; }
    public int Version { get; set; }
    public string CorrelationId { get; set; }
    public object Data { get; set; }

    public static EventEnvelope Create<T>(T data)
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = typeof(T).Name,
            Version = 1,
            CorrelationId = CorrelationContext.CurrentId,
            Data = data
        };
    }
}
```

---

## 6. 🧾 Outbox Module

---

### Interface

```
public interface IOutboxService
{
    Task AddAsync(EventEnvelope envelope);
}
```

---
### Usage

```
await _outbox.AddAsync(envelope);
```

---

---

### EF Core Integration

```
modelBuilder.Entity<OutboxEvent>()
    .HasIndex(x => new { x.Status, x.CreatedAt });
```

---

## 7. 🔄 Kafka Producer Module
---
### Interface

```
public interface IKafkaProducer
{
    Task PublishAsync(string topic, string key, object value);
}
```

---

### Implementation

```
public class KafkaProducer : IKafkaProducer
{
    private readonly IProducer<string, string> _producer;

    public async Task PublishAsync(string topic, string key, object value)
    {
        var json = JsonSerializer.Serialize(value);

        await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = json
        });
    }
}
```

## 8. 🧪 Kafka Consumer Base

```
public abstract class KafkaConsumerBase<T>
{
    public async Task HandleAsync(EventEnvelope envelope)
    {
        if (await IsDuplicate(envelope.EventId))
            return;

        await ProcessAsync((T)envelope.Data);

        await MarkProcessed(envelope.EventId);
    }

    protected abstract Task ProcessAsync(T data);
}
```

---

## 9. 🔐 Idempotency Module

---

### Interface

```
public interface IIdempotencyStore
{
    Task<bool> ExistsAsync(string key);
    Task MarkProcessedAsync(string key);
}
```

---

### Redis Implementation

```
public class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IDatabase _redis;

    public async Task<bool> ExistsAsync(string key)
        => await _redis.KeyExistsAsync(key);

    public async Task MarkProcessedAsync(string key)
        => await _redis.StringSetAsync(key, "1", TimeSpan.FromDays(7));
}
```

## 10. 🔁 Resilience Module

---

### Retry (Polly)
```
Policy
  .Handle<Exception>()
  .WaitAndRetryAsync(5, retry =>
      TimeSpan.FromSeconds(Math.Pow(2, retry)));
```

---

### Circuit Breaker

```
Policy
  .Handle<Exception>()
  .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
```

---

## 11. 📊 Observability Module

---

### Logging

- Serilog integration

---

---

### Tracing

- OpenTelemetry auto instrumentation

---

---

### Correlation

```
public static class CorrelationContext
{
    public static string CurrentId => Activity.Current?.TraceId.ToString();
}
```

---

## 12. ⚙️ Dependency Injection Setup
---

```
public static IServiceCollection AddInfrastructure(this IServiceCollection services)
{
    services.AddSingleton<IEventBus, KafkaEventBus>();
    services.AddSingleton<IKafkaProducer, KafkaProducer>();
    services.AddSingleton<IIdempotencyStore, RedisIdempotencyStore>();
    services.AddScoped<IOutboxService, OutboxService>();

    return services;
}
```

---

## 13. 🧪 Usage Example (Order Service)
---
```
public async Task Handle(CreateOrderCommand cmd)
{
    var order = Order.Create(cmd);

    var evt = new OrderCreatedEvent(order.Id);

    await _outbox.AddAsync(EventEnvelope.Create(evt));

    await _db.SaveChangesAsync();
}
```

---

## 14. ⚠️ Versioning Strategy
---
Shared lib phải backward compatible

---

---

### Rule

Breaking change → version mới

---

---

## 15. ⚠️ Anti-patterns

---

❌ Nhét business logic vào shared lib  
❌ Hardcode topic  
❌ Không version  
❌ Không test  
❌ Coupling quá chặt

---

---

## 16. 🎯 Design Guarantees

---

Base library đảm bảo:

- Consistent giữa services
- Giảm duplicate code
- Dễ maintain
- Dễ scale team

---

---

# ✅ Kết luận

Base library là:

> **“Nền tảng kỹ thuật giúp toàn bộ microservices nói cùng một ngôn ngữ”**

---

## Insight quan trọng nhất

> **Nếu không có shared infra chuẩn — mỗi service sẽ trở thành một “hệ thống riêng lẻ”**