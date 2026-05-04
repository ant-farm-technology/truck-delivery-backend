## 1. 🎯 Mục tiêu

- Minh hoạ interaction end-to-end
- Thể hiện async boundaries (Kafka)
- Bao gồm cả happy path và failure paths
- Là tài liệu tham chiếu cho dev + architect

---

## 2. 🧭 Actors & Services

Customer App (NextJS / Flutter)  
Driver App (Flutter)  
  
API Gateway (YARP)  
  
Order Service (.NET)  
Routing Service (Rust)  
Optimization Service (Python)  
Fleet Service (.NET)  
Tracking Service (.NET)  
Payment Service (.NET)  
Notification Service (.NET)  
  
Kafka (Event Backbone)

---

---

## 3. 🚀 Happy Path Sequence

```
sequenceDiagram
    autonumber

    participant C as Customer App
    participant G as API Gateway
    participant O as Order Service
    participant K as Kafka
    participant R as Routing Service
    participant X as Optimization Service
    participant F as Fleet Service
    participant T as Tracking Service
    participant D as Driver App
    participant P as Payment Service
    participant N as Notification Service

    C->>G: POST /orders
    G->>O: CreateOrder()

    O->>O: Save Order (MySQL)
    O->>K: OrderCreated

    K-->>R: OrderCreated
    R->>R: Calculate Route (PostGIS)
    R->>K: RouteCalculated

    K-->>X: OrderCreated / RouteCalculated
    X->>X: Solve assignment (OR-Tools)
    X->>K: DriverAssigned

    K-->>F: DriverAssigned
    F->>F: Update Driver=BUSY

    K-->>O: DriverAssigned
    O->>O: Status = ASSIGNED
    O->>K: OrderAssigned

    K-->>T: OrderAssigned
    T->>T: Create tracking session

    D->>T: Send location (stream)
    T-->>C: Realtime location (SignalR)

    D->>G: POST /orders/{id}/pickup
    G->>O: Pickup()

    O->>O: Status = PICKED_UP
    O->>K: OrderPickedUp
    O->>N: Notify customer

    D->>G: POST /orders/{id}/deliver
    G->>O: Deliver()

    O->>O: Status = DELIVERED
    O->>K: OrderDelivered

    K-->>P: OrderDelivered
    P->>P: Process payment
    P->>K: PaymentCompleted

    K-->>O: PaymentCompleted
    O->>O: Status = COMPLETED

    O->>N: Notify completion
```

## 4. ⚠️ Failure Path #1 — Outbox / Kafka publish fail

```
sequenceDiagram
    participant O as Order Service
    participant DB as MySQL
    participant Outbox as Outbox Table
    participant K as Kafka

    O->>DB: Save Order
    O->>Outbox: Save OrderCreated event

    Note over O: Kafka down

    loop Retry background job
        Outbox->>K: Publish event
    end

    Note over K: Eventually success
```

### Insight

Không bao giờ mất event

## 5. ⚠️ Failure Path #2 — Optimization timeout

```
sequenceDiagram
    participant K as Kafka
    participant X as Optimization Service

    K-->>X: OrderCreated

    X->>X: Run solver

    alt Timeout
        X->>X: Fallback (nearest driver)
        X->>K: DriverAssignmentFallback
    else Success
        X->>K: DriverAssigned
    end
```

## 6. ⚠️ Failure Path #3 — Driver không phản hồi

```
sequenceDiagram
    participant X as Optimization
    participant F as Fleet
    participant D as Driver
    participant K as Kafka

    X->>K: DriverAssigned

    K-->>F: DriverAssigned
    F->>D: Notify driver

    Note over D: No response

    F->>F: Timeout (30s)

    F->>K: DriverAssignmentExpired
    K-->>X: Retry assignment
```

## 7. ⚠️ Failure Path #4 — Payment fail

```
sequenceDiagram
    participant P as Payment Service
    participant K as Kafka
    participant O as Order Service

    K-->>P: OrderDelivered

    P->>P: Call payment gateway

    alt Failed
        P->>K: PaymentFailed
    else Success
        P->>K: PaymentCompleted
    end

    K-->>O: PaymentFailed
    O->>O: Status = PAYMENT_PENDING
```

## 8. ⚠️ Failure Path #5 — Kafka consumer crash

```
sequenceDiagram
    participant K as Kafka
    participant C as Consumer

    K-->>C: Event

    C->>C: Process

    Note over C: Crash before commit offset

    K-->>C: Re-deliver event

    C->>C: Idempotency check
```

## 9. 🔄 Retry + DLQ Flow

```
sequenceDiagram
    participant C as Consumer
    participant K as Kafka
    participant DLQ as Dead Letter Topic

    K-->>C: Event

    loop Retry (max 5)
        C->>C: Process
    end

    alt Still fail
        C->>DLQ: Send event
    end
```

## ## 10. 🧠 Async Boundaries (Quan trọng nhất)

---

API → Order Service → Kafka → ALL downstream

---

---

### Key insight

Không có synchronous chain dài

---

---

## 11. ⏱️ Timing Expectations

---

|Step|Expected Time|
|---|---|
|Create Order|< 100ms|
|Dispatch|100ms – 2s|
|Optimization|200ms – 5s|
|Tracking update|real-time|
|Payment|1–3s|

---

---

## 12. 🎯 Design Guarantees

---

Sequence này đảm bảo:

- Không block giữa services
- Retry-safe
- Idempotent
- Fault-tolerant
- Eventually consistent

---

---

# ✅ Kết luận

Sequence diagram này thể hiện:

> **Một hệ thống event-driven thực sự — nơi Kafka là trung tâm điều phối, không phải API synchronous**

---

## Insight quan trọng nhất

> **Bạn không “call service khác” — bạn “emit event và để hệ thống tự tiến hoá”**