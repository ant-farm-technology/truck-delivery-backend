## 1. 🎯 Mục tiêu

Thiết lập chuẩn cho:

- Giao tiếp giữa các service qua event
- Đảm bảo **loose coupling + high resilience**
- Hỗ trợ **scaling + failure recovery**

---

## 2. 🧠 Nguyên tắc cốt lõi

---

### 2.1 Event = Fact (không phải command)

✔ OrderCreated → fact  
❌ CreateOrder → command (không dùng trong event bus)

👉 Event phải là **điều đã xảy ra**, không phải yêu cầu.

---

### 2.2 Immutability (bắt buộc)

Event đã publish → KHÔNG được sửa

Nếu thay đổi:

- Tạo event mới (version mới)

---

### 2.3 At-least-once delivery

Kafka đảm bảo:

Event có thể bị gửi nhiều lần

👉 Tất cả consumer phải:

IDEMPOTENT

---

### 2.4 Loose Coupling

- Producer không biết consumer là ai
- Consumer không phụ thuộc producer implementation

---

### 2.5 Eventual Consistency

- Không đảm bảo sync ngay lập tức
- Chấp nhận delay (milliseconds → seconds)

---

## 3. 🧩 Event Taxonomy

---

### 3.1 Domain Events (chính)

Phản ánh business:

OrderCreated  
DriverAssigned  
RouteGenerated

---

### 3.2 Integration Events

Dùng giữa bounded contexts:

DispatchRequested  
OptimizationCompleted

---

### 3.3 System Events (ít dùng)

ServiceStarted  
CacheInvalidated

## 4. Event Naming Convention

---

### Format

<domain>.<entity>.<event>.v<version>

---

### Ví dụ

order.order.created.v1  
shipment.dispatch.requested.v1  
routing.route.generated.v1  
driver.driver.assigned.v1

---

### Rules

- lowercase  
- không dùng dấu cách  
- version bắt buộc

---

## 5. Event Envelope (chuẩn bắt buộc)

Mọi event phải có cấu trúc:

{  
  "eventId": "uuid",  
  "eventType": "order.order.created.v1",  
  "occurredAt": "2026-04-27T10:00:00Z",  
  "producer": "order-service",  
  "correlationId": "uuid",  
  "causationId": "uuid",  
  "payload": { }  
}

---

### Ý nghĩa

|Field|Mục đích|
|---|---|
|eventId|deduplication|
|correlationId|trace toàn flow|
|causationId|event cha|
|producer|debug|
|payload|dữ liệu|

---

## 6. Event Catalog (core events)

---

### Order Domain

{  
  "eventType": "order.order.created.v1",  
  "payload": {  
    "orderId": "uuid",  
    "pickupLocation": { "lat": 0, "lng": 0 },  
    "deliveryLocation": { "lat": 0, "lng": 0 },  
    "timeWindow": {},  
    "capacity": {}  
  }  
}

---

### Dispatch Domain

{  
  "eventType": "shipment.dispatch.requested.v1",  
  "payload": {  
    "dispatchId": "uuid",  
    "orderIds": [],  
    "vehicleIds": []  
  }  
}

---

### Routing

{  
  "eventType": "routing.route.generated.v1",  
  "payload": {  
    "routes": [],  
    "distanceMatrixId": "uuid"  
  }  
}

---

### Optimization

{  
  "eventType": "optimization.completed.v1",  
  "payload": {  
    "routes": [],  
    "cost": 1234  
  }  
}

---

## 7. Kafka Topic Design

---

### Topic Naming

<domain>.<entity>

---

### Ví dụ

order.order  
shipment.dispatch  
routing.route  
optimization.result  
driver.driver

---

### Partition Strategy

|Topic|Key|
|---|---|
|order.order|orderId|
|shipment.dispatch|dispatchId|
|routing.route|routeId|
|optimization.result|dispatchId|

---

Rule:

Same key → same partition → đảm bảo order

---

## 8. Retry & DLQ Strategy

---

### Retry Policy

- Exponential backoff
- Max retry: 3–5 lần

---

### DLQ (Dead Letter Queue)

Mỗi topic phải có DLQ:

order.order.dlq  
shipment.dispatch.dlq

---

### Khi nào vào DLQ

- Payload invalid
- Business rule fail
- Timeout

---

### Xử lý DLQ

- Manual replay
- Alert monitoring

## 9. Idempotency Strategy

---

### Bắt buộc cho mọi consumer

---

### Cách làm

#### Option 1: Redis

Key: event:{eventId}  
TTL: 24h

---

#### Option 2: DB table

ProcessedEvents(eventId)

---

### Rule

Nếu eventId đã tồn tại → skip

---

## 10. Saga Pattern (Choreography)

---

### Flow chính

OrderCreated  
   ↓  
Shipment Service  
   ↓  
DispatchRequested  
   ↓  
RouteRequested  
   ↓  
RouteGenerated  
   ↓  
OptimizationCompleted  
   ↓  
DriverAssigned

---

### Nguyên tắc

- Không có central orchestrator  
- Mỗi service tự phản ứng event

---

## 11. Failure Handling

---

### Case 1: Optimizer fail

→ emit OptimizationFailed  
→ retry hoặc fallback

---

### Case 2: Driver không nhận

→ emit DriverRejected  
→ re-dispatch

---

### Case 3: Event mất thứ tự

→ dùng partition key  
→ hoặc version check

---

## 12. Observability

---

### Tracing

- Dùng `correlationId` xuyên suốt

---

### Logging

log(eventId, correlationId, eventType)

---

### Metrics

- Event throughput
- Consumer lag
- DLQ size

---

## 13. Security

---

- Không chứa PII nhạy cảm trong event
- Encrypt nếu cần
- Validate schema trước khi publish

---

## 14. Anti-patterns (TRÁNH)

---

Event quá lớn (payload nặng)  
Sync call sau khi publish event  
Không version event  
Consumer không idempotent  
Shared DB thay vì event

---

## 15. Design Guarantees

Hệ thống đảm bảo:

- Không mất event (Kafka)
- Có thể replay event
- Scale consumer độc lập
- Fault isolation giữa services