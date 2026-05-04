## 1. 🎯 Mục tiêu

Template này giúp:

- Consume event từ Apache Kafka một cách an toàn
- Tránh duplicate processing
- Retry đúng cách
- Không làm crash service
- Dễ mở rộng & maintain

---

## 2. 🧠 Nguyên tắc thiết kế

---

### 2.1 At-least-once

Event có thể bị gửi lại

---

---

### 2.2 Idempotent handler

Process nhiều lần vẫn đúng

---

---

### 2.3 Không throw unhandled exception

Crash = mất control retry

---

---

### 2.4 Offset commit sau khi xử lý thành công

Không commit sớm

---

---

## 3. 🧱 Consumer Flow (Chuẩn)

---

Receive event  
  ↓  
Deserialize  
  ↓  
Idempotency check  
  ↓  
Process business logic  
  ↓  
Commit offset

---

---

## 4. ⚙️ Cấu trúc Consumer

---

### Components

- Consumer loop
- Event handler
- Idempotency store
- Retry handler
- DLQ publisher

---

---

## 5. 🧾 Event Envelope

---

{  
  "eventId": "uuid",  
  "eventType": "OrderCreated",  
  "timestamp": "2026-01-01T00:00:00Z",  
  "data": {}  
}

---

---

## 6. 🧪 Idempotency Strategy

---

### Key

eventId

---

---

### Storage

- Redis (fast)
- MySQL (durable)

---

---

### Logic

if processed(eventId) → skip  
else → process + mark processed

---

---

## 7. 🔄 Retry Strategy

---

### Approach

Retry in-memory (short)  
Retry via Kafka (requeue)

---

---

### Backoff

1s → 2s → 5s → 10s

---

---

### Limit

max 5 retries

---

---

## 8. 🪤 DLQ Strategy

---

### Topic

{topic}.dlq

---

---

### Payload

{  
  "event": {},  
  "error": "string",  
  "retryCount": 5  
}

---

---

## 9. ⚠️ Error Handling

---

### Types

---

#### Transient

Retry

---

---

#### Permanent

Send to DLQ

---

---

## 10. 🧪 .NET Consumer Template (Production)

---
```
public class KafkaConsumerWorker : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IEventHandler _handler;
    private readonly IIdempotencyStore _idempotency;
    private readonly IDlqPublisher _dlq;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe("order.events");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = _consumer.Consume(stoppingToken);

            try
            {
                var envelope = Deserialize(result.Message.Value);

                // Idempotency check
                if (await _idempotency.ExistsAsync(envelope.EventId))
                    continue;

                await ProcessWithRetry(envelope);

                await _idempotency.MarkProcessedAsync(envelope.EventId);

                _consumer.Commit(result);
            }
            catch (Exception ex)
            {
                await _dlq.PublishAsync(result.Message.Value, ex.Message);
            }
        }
    }

    private async Task ProcessWithRetry(EventEnvelope envelope)
    {
        int retry = 0;

        while (true)
        {
            try
            {
                await _handler.HandleAsync(envelope);
                return;
            }
            catch (TransientException)
            {
                retry++;

                if (retry > 5)
                    throw;

                await Task.Delay(GetBackoff(retry));
            }
        }
    }
}
```

## 11. 🧠 Handler Pattern

```
public class OrderCreatedHandler : IEventHandler
{
    public async Task HandleAsync(EventEnvelope envelope)
    {
        var data = envelope.Data.ToObject<OrderCreated>();

        // Business logic
        await _service.ProcessAsync(data);
    }
}
```

## 12. 📊 Observability

---

### Logging

- Log eventId
- Log retry count

---

---

### Metrics

- Processing time
- Error rate
- Retry count

---

---

### Tools

- Prometheus
- Grafana

---

---

## 13. ⚡ Performance Tips

---

- Batch consume  
- Parallel processing (careful with ordering)  
- Use async I/O

---

---

## 14. ⚠️ Ordering Consideration

---

Ordering chỉ đảm bảo trong partition

---

---

### Rule

1 partition → 1 consumer thread

---

---

## 15. 🔄 Scaling Strategy

---

Scale consumer instances  
≤ partition count

---

---

## 16. ⚠️ Anti-patterns

---

❌ Commit offset trước khi xử lý  
❌ Không idempotency  
❌ Retry vô hạn  
❌ Throw exception không catch  
❌ Không DLQ

---

---

## 17. 🎯 Design Guarantees

---

Template này đảm bảo:

- Không mất event
- Không duplicate effect
- Retry an toàn
- Recover dễ dàng

---

---

# ✅ Kết luận

Kafka consumer chuẩn là:

> **Idempotent + Retry-safe + Observable**

---

## Insight quan trọng nhất

> **Consumer không phải “đọc message” — mà là “xử lý business một cách an toàn trong môi trường unreliable”**