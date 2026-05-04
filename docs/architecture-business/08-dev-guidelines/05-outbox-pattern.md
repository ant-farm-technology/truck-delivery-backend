## 1. 🎯 Mục tiêu

Giải quyết bài toán:

> **“Lưu DB thành công nhưng publish event thất bại”**

Outbox Pattern đảm bảo:

- Không mất event
- Consistent giữa DB và Kafka
- Hỗ trợ retry an toàn

---

## 2. 🚨 Problem (Nếu không dùng Outbox)

---

Save Order (MySQL) ✔  
Publish Kafka ❌ (network fail)  
  
→ Order tồn tại  
→ Nhưng downstream KHÔNG biết  
→ System bị lệch trạng thái

---

## 3. 🧠 Ý tưởng cốt lõi

---

DB transaction = Business Data + Event

---

---

### Flow

Write DB  
  + Write Outbox Table  
        ↓  
Background Worker  
        ↓  
Publish Kafka  
        ↓  
Mark as processed

---

---

## 4. 🧱 Kiến trúc

---

Order Service  
  
[Application Layer]  
        ↓  
[EF Core Transaction]  
        ├── Orders table  
        └── Outbox table  
                ↓  
        Background Worker  
                ↓  
        Kafka Producer

---

---

## 5. 🧾 Outbox Table Design

---

CREATE TABLE outbox_events (  
    id CHAR(36) PRIMARY KEY,  
    aggregate_id CHAR(36),  
    event_type VARCHAR(100),  
    payload JSON,  
    status VARCHAR(20), -- PENDING / PROCESSED / FAILED  
    retry_count INT DEFAULT 0,  
    created_at TIMESTAMP,  
    processed_at TIMESTAMP NULL  
);

---

---

### Index

CREATE INDEX idx_outbox_status_created  
ON outbox_events(status, created_at);

---

---

## 6. 🧪 Write Flow (Transaction)

```
public async Task CreateOrderAsync(CreateOrderCommand cmd)
{
    var order = Order.Create(cmd);

    var outbox = OutboxEvent.Create(
        aggregateId: order.Id,
        eventType: "OrderCreated",
        payload: JsonSerializer.Serialize(order)
    );

    _dbContext.Orders.Add(order);
    _dbContext.OutboxEvents.Add(outbox);

    await _dbContext.SaveChangesAsync(); // atomic
}
```

---

### Guarantee

Order + Event luôn đi cùng nhau

---

---

## 7. ⚙️ Background Publisher

---

### Worker Loop

```
public class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var kafka = scope.ServiceProvider.GetRequiredService<IKafkaProducer>();

            var events = await db.OutboxEvents
                .Where(x => x.Status == "PENDING")
                .OrderBy(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            foreach (var evt in events)
            {
                try
                {
                    await kafka.PublishAsync(evt.EventType, evt.Payload);

                    evt.Status = "PROCESSED";
                    evt.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception)
                {
                    evt.RetryCount++;

                    if (evt.RetryCount > 5)
                        evt.Status = "FAILED";
                }
            }

            await db.SaveChangesAsync();

            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

## 8. 🔄 Retry Strategy

---

### Policy

RetryCount ≤ 5  
Backoff tăng dần

---

---

### State machine

PENDING → PROCESSED  
        → FAILED (sau max retry)

---

---

## 9. 🪤 Failed Events Handling

---

### Option 1

Manual retry

---

---

### Option 2

Move to Kafka DLQ

---

---

### Option 3

Alert + dashboard

---

---

## 10. 🔐 Idempotency (Producer Side)

---

### Problem

Retry publish → duplicate event

---

---

### Solution

eventId (Outbox id)

---

---

### Consumer side

Idempotency check

---

---

## 11. ⚡ Performance Optimization

---

### Batch publish

Publish multiple events / batch

---

---

### Parallelism

Multiple workers (partition-safe)

---

---

### Cleanup

DELETE FROM outbox_events  
WHERE status = 'PROCESSED'  
AND created_at < NOW() - INTERVAL 7 DAY;

---

---

## 12. 📊 Observability

---

### Metrics

- pending events
- failed events
- publish latency

---

---

### Logging

- eventId
- retry count

---

---

### Tools

- Prometheus
- Grafana

---

---

## 13. ⚠️ Common Pitfalls

---

❌ Không retry  
❌ Không cleanup outbox  
❌ Publish trong transaction (anti-pattern)  
❌ Không index  
❌ Không giới hạn batch size

---

---

## 14. 🔄 Alternative Approaches

---

|Approach|Trade-off|
|---|---|
|Direct publish|Nhanh nhưng mất event|
|2PC|Complex, không scalable|
|CDC (Debezium)|Powerful nhưng phức tạp|

---

---

## 15. 🎯 Design Guarantees

---

Outbox Pattern đảm bảo:

- Không mất event
- Event luôn consistent với DB
- Retry an toàn
- Dễ debug & replay

---

---

# ✅ Kết luận

Outbox Pattern là:

> **“Cầu nối an toàn giữa Database và Kafka”**

---

## Insight quan trọng nhất

> **Bạn không bao giờ publish event trực tiếp từ business logic — luôn đi qua Outbox**