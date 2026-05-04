## 1. Mục tiêu

Tài liệu này định nghĩa:

- Cách sử dụng Apache Kafka trong hệ thống
- Thiết kế topic / partition / consumer group
- Đảm bảo throughput, ordering, reliability
- Strategy cho scale & failure handling

---

## 2. Nguyên tắc thiết kế

---

### 2.1 Event-first

Mọi interaction giữa services → qua Kafka

---

---

### 2.2 Domain-based topics

Không dùng 1 topic chung

---

---

### 2.3 Partition-aware design

Ordering chỉ đảm bảo trong 1 partition

---

---

### 2.4 At-least-once delivery

Có thể duplicate → phải idempotent

---

---

## 3. Topic Design

---

## 3.1 Naming Convention

{domain}.{entity}.{type}  
  
Ví dụ:  
order.events  
dispatch.events  
tracking.events  
payment.events

---

---

## 3.2 Topic List

---

### Order

order.events

Events:

- OrderCreated
- OrderAssigned
- OrderPickedUp
- OrderDelivered
- OrderCompleted

---

---

### Routing

routing.events

Events:

- RouteCalculated

---

---

### Dispatch

dispatch.events

Events:

- DriverAssigned

---

---

### Tracking

tracking.events

Events:

- LocationUpdated

---

---

### Payment

payment.events

Events:

- PaymentCompleted
- PaymentFailed

---

---

### Notification

notification.events

---

---

## 4. Partition Strategy

---

### 4.1 Key Selection

---

#### Order-related events

key = orderId

---

---

#### Driver-related events

key = driverId

---

---

### 4.2 Why?

→ đảm bảo ordering theo entity

---

---

## 4.3 Partition Count (initial)

---

|Topic|Partitions|
|---|---|
|order.events|12|
|dispatch.events|12|
|routing.events|6|
|tracking.events|24|
|payment.events|6|

---

---

### Rule

Partitions ≥ max consumer instances

---

---

## 5. Consumer Groups

---

### Rule

Mỗi service = 1 consumer group

---

---

### Example

---

#### Order Service

Consumes:  
- DriverAssigned  
- PaymentCompleted

---

---

#### Dispatch Service

Consumes:  
- OrderCreated

---

---

#### Routing Service

Consumes:  
- OrderCreated

---

---

#### Tracking Service

Consumes:  
- OrderAssigned

---

---

## 6. Throughput Design

---

### Estimation

---

#### Tracking

10k drivers  
× 1 event / 2s  
≈ 5k events/sec

---

---

#### Order events

~100–500 events/sec

---

---

### Strategy

- Increase partitions  
- Scale consumers

---

---

## 7. Producer Strategy

---

### Outbox Pattern (bắt buộc)

---

DB transaction → Outbox → Kafka

---

---

### Benefit

Không mất event

---

---

## 8. Consumer Strategy

---

### 8.1 Idempotency

---

Xử lý duplicate event

---

---

### 8.2 Retry

---

Retry với backoff

---

---

### 8.3 Dead Letter Queue (DLQ)

---

event.fail → *.dlq

---

---

## 9. Ordering Guarantee

---

✔ ordering theo key
❌ không global ordering

---

---

## 10. Replay Strategy

---

### Use case

- Bug fix  
- Rebuild projection

---

---

### Method

Reset offset

---

---

## 11. Reliability

---

### Replication

replication.factor = 3

---

---

### Ack

acks = all

---

---

### Min ISR

min.insync.replicas = 2

---

---

## 12. Retention Policy

---

### Event topics

7–14 days

---

---

### Tracking (high volume)

1–3 days

---

---

### DLQ

≥ 14 days

---

---

## 13. Monitoring Kafka

---

### Metrics

- consumer lag
- throughput
- error rate

---

---

### Tools

- Prometheus
- Grafana

---

---

## 14. Hot Partition Problem

---

### Case

1 key có traffic lớn

---

---

### Solution

- key = orderId + suffix  
- load balancing

---

---

## 15. Schema Management

---

### Format

JSON / Avro

---

---

### Rule

Backward compatible

---

---

## 16. Anti-patterns

---

❌ 1 topic cho tất cả event  
❌ Không dùng key  
❌ Không idempotency  
❌ Không DLQ  
❌ Consumer quá ít partition

---

---

## 17. Design Guarantees

---

Kafka topology đảm bảo:

- Scale tuyến tính
- Không mất event
- Service decoupled
- Replay được

---

---

# Kết luận

Kafka trong hệ thống này:

> **Không chỉ là message broker — mà là event backbone**

---

## Insight quan trọng nhất

> **Thiết kế partition & key quyết định khả năng scale của toàn hệ thống**