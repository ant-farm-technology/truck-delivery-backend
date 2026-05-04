## 1. 🎯 Mục tiêu

Tài liệu này định nghĩa:

- Các resilience patterns áp dụng trong hệ thống
- Khi nào dùng pattern nào
- Cách kết hợp chúng trong kiến trúc microservices + event-driven
- Đảm bảo hệ thống **degrade gracefully** thay vì crash

---

## 2. 🧠 Nguyên tắc cốt lõi

---

### 2.1 Assume failure

Mọi thứ đều có thể fail (network, DB, Kafka, service)

---

---

### 2.2 Fail fast, recover async

Không block request lâu → xử lý async

---

---

### 2.3 Isolation > Global stability

1 service fail không được kéo sập hệ thống

---

---

### 2.4 Idempotency everywhere

Retry phải an toàn

---

---

## 3. 🧱 Core Resilience Patterns

---

# 3.1 🔄 Retry Pattern

---

## Khi dùng

- External API
- DB transient error
- Kafka publish/consume

---

## Strategy

Retry + exponential backoff

---

### Example

1s → 2s → 5s → 10s → fail

---

---

## Rule

Không retry vô hạn

---

---

## Implementation

- .NET: Polly
- Python: tenacity

---

---

# 3.2 🚫 Circuit Breaker

---

## Khi dùng

- External services (payment gateway)
- DB bị quá tải

---

---

## Behavior

Closed → Open → Half-open

---

---

## Flow

Fail nhiều → Open (block request)  
↓  
Cooldown  
↓  
Half-open → test

---

---

## Benefit

Tránh cascade failure

---

---

# 3.3 🧱 Bulkhead Pattern

---

## Khi dùng

- Service có nhiều loại workload

---

---

## Idea

Tách resource (thread, connection)

---

---

## Example

Tracking ≠ Payment

---

---

## Benefit

1 phần fail không ảnh hưởng phần khác

---

---

# 3.4 ⏱️ Timeout Pattern

---

## Khi dùng

- External calls
- Optimization solver

---

---

## Rule

Timeout luôn phải có

---

---

## Example

Routing: 2s  
Optimization: 5s

---

---

## Fallback

Timeout → fallback logic

---

---

# 3.5 🧾 Outbox Pattern

---

## Problem

Save DB nhưng không publish Kafka

---

---

## Solution

DB + Outbox → background publish

---

---

## Benefit

Không mất event

---

---

# 3.6 📦 Inbox / Idempotency Pattern

---

## Problem

Duplicate Kafka message

---

---

## Solution

Store processed eventId

---

---

## Storage

- Redis
- DB

---

---

## Rule

Processed → skip

---

---

# 3.7 🔄 Saga Pattern

---

## Khi dùng

- Distributed transaction

---

---

## Idea

Step-by-step + compensation

---

---

## Example

Assign driver → fail → rollback

---

---

## Type

- Choreography (Kafka events)
- Orchestration (central coordinator)

---

---

# 3.8 🪤 Dead Letter Queue (DLQ)

---

## Khi dùng

Retry hết nhưng vẫn fail

---

---

## Flow

Event → Retry → Fail → DLQ

---

---

## Benefit

Không mất data

---

---

# 3.9 🔁 Replay Pattern

---

## Khi dùng

- Bug fix
- Rebuild projection

---

---

## Method

Reset Kafka offset

---

---

# 3.10 🧠 Fallback Pattern

---

## Khi dùng

- Routing fail
- Optimization fail

---

---

## Example

OR-Tools fail → nearest driver

---

---

## Rule

Có fallback cho critical flow

---

---

# 3.11 📊 Load Shedding

---

## Khi dùng

- Traffic spike

---

---

## Strategy

Drop low-priority request

---

---

## Example

Tracking overload → giảm frequency

---

---

# 3.12 🧵 Backpressure

---

## Khi dùng

- Kafka consumer lag

---

---

## Strategy

Throttle producer / scale consumer

---

---

## 4. 🔗 Pattern Mapping theo System

---

|Component|Patterns|
|---|---|
|Order Service|Outbox, Retry, Idempotency|
|Routing|Timeout, Fallback|
|Optimization|Timeout, Fallback|
|Fleet|Idempotency|
|Tracking|Bulkhead, Load shedding|
|Payment|Circuit breaker, Retry|
|Kafka consumers|Retry, DLQ, Replay|

---

---

## 5. ⚠️ Anti-patterns

---

❌ Retry vô hạn  
❌ Không timeout  
❌ Không fallback  
❌ Sync chain dài  
❌ Không DLQ  
❌ Không idempotency

---

---

## 6. 🧠 Design Strategy (Quan trọng)

---

### Priority

1. Không mất data  
2. Không crash system  
3. Recover được  
4. Optimize sau

---

---

## 7. 📊 Observability cho Resilience

---

### Detect issues

- Retry rate
- Error rate
- Circuit breaker state
- Kafka lag

---

---

### Tools

- Prometheus
- Grafana

---

---

## 8. 🎯 Design Guarantees

---

Resilience patterns đảm bảo:

- System không gãy dây chuyền
- Có thể recover sau failure
- Scale ổn định
- User vẫn có experience chấp nhận được

---

---

# ✅ Kết luận

Resilience không phải là “feature”

> **Nó là nền tảng để hệ thống sống sót**

---

## Insight quan trọng nhất

> **Bạn không thể tránh failure — bạn chỉ có thể thiết kế để sống chung với nó**