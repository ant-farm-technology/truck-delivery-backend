## 1. Mục tiêu

Event Catalog định nghĩa:

- Danh sách toàn bộ events
- Schema chuẩn
- Producer / Consumer
- Versioning & compatibility

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Event = Fact (điều đã xảy ra)

 → OrderCreated  
 → PaymentCompleted  
  
x → CreateOrder  
x → UpdateDriver

---

### 2.2 Immutable

Event không được sửa sau khi publish

---

### 2.3 Backward-compatible

- Chỉ add field  
- Không remove / rename

---

---

## 3. Event Envelope (CHUẨN BẮT BUỘC)

---

### Schema

{  
  "eventId": "uuid",  
  "eventType": "OrderCreated",  
  "version": 1,  
  "occurredAt": "2026-01-01T00:00:00Z",  
  "correlationId": "uuid",  
  "producer": "order-service",  
  "payload": { }  
}

---

### Field ý nghĩa

|Field|Description|
|---|---|
|eventId|unique|
|eventType|tên event|
|version|schema version|
|correlationId|trace request|
|producer|service phát|

---

---

## 4. Naming Convention

---

<Context><ActionPast>  
  
OrderCreated  
DriverAssigned  
PaymentCompleted

---

---

## 5. Order Events

---

### 5.1 OrderCreated

{  
  "orderId": "uuid",  
  "pickup": { "lat": 0, "lng": 0 },  
  "delivery": { "lat": 0, "lng": 0 }  
}

---

- Producer: Order Service
- Consumers:
    - Dispatch
    - Shipment

---

---

### 5.2 OrderAssigned

{  
  "orderId": "uuid",  
  "driverId": "uuid"  
}

---

- Producer: Dispatch
- Consumers:
    - Fleet
    - Tracking
    - Notification

---

---

### 5.3 OrderDelivered

{  
  "orderId": "uuid",  
  "deliveredAt": "timestamp"  
}

---

- Producer: Order / Tracking
- Consumers:
    - Payment
    - Notification

---

---

## 6. Fleet Events

---

### 6.1 DriverRegistered

{  
  "driverId": "uuid",  
  "userId": "uuid"  
}

---

- Producer: Fleet
- Consumers:
    - Notification

---

---

### 6.2 DriverAvailable

{  
  "driverId": "uuid"  
}

---

- Producer: Fleet
- Consumers:
    - Dispatch

---

---

### 6.3 DriverAssigned

{  
  "driverId": "uuid",  
  "orderId": "uuid"  
}

---

- Producer: Dispatch
- Consumers:
    - Fleet
    - Tracking

---

---

## 7. Routing Events (optional)

---

Thường không publish  
→ sync request/response

---

---

## 8. Optimization Events

---

### 8.1 OptimizationRequested

{  
  "batchId": "uuid",  
  "orderIds": []  
}

---

- Producer: Dispatch
- Consumers:
    - Optimizer

---

---

### 8.2 OptimizationCompleted

{  
  "batchId": "uuid",  
  "routes": []  
}

---

- Producer: Optimizer
- Consumers:
    - Dispatch

---

---

## 9. Tracking Events

---

### 9.1 LocationUpdated

{  
  "driverId": "uuid",  
  "lat": 0,  
  "lng": 0,  
  "timestamp": "..."  
}

---

- Producer: Tracking
- Consumers:
    - Notification (optional)
    - Analytics

---

---

## 10. Payment Events

---

### 10.1 PaymentCreated

{  
  "paymentId": "uuid",  
  "orderId": "uuid",  
  "amount": 100  
}

---

---

### 10.2 PaymentCompleted

{  
  "paymentId": "uuid",  
  "orderId": "uuid"  
}

---

- Consumers:
    - Notification

---

---

### 10.3 PaymentFailed

{  
  "paymentId": "uuid",  
  "reason": "..."  
}

---

---

## 11. Notification Events (internal)

---

Thường không expose ra Kafka  
→ internal processing

---

---

## 12. Identity Events

---

### 12.1 UserRegistered

{  
  "userId": "uuid",  
  "role": "Driver"  
}

---

- Consumers:
    - Fleet (create driver profile)

---

---

## 13. Kafka Topic Design

---

### Strategy

topic = context.events

---

### Examples

order.events  
fleet.events  
payment.events  
tracking.events

---

---

## 14. Versioning Strategy

---

### Rule

- version field trong envelope  
- không break schema

---

### Khi cần breaking change

→ tạo event mới (v2)

---

---

## 15. Idempotency (Consumer)

---

### Rule

Mỗi consumer phải idempotent

---

### Strategy

- Store processed eventId  
- Skip nếu duplicate

---

---

## 16. Ordering

---

### Problem

Event đến sai thứ tự

---

### Solution

- Partition theo key (orderId)

---

---

## 17. Delivery Semantics

---

Kafka = at-least-once

---

Implication:

Consumer phải chịu duplicate

---

---

## 18. Anti-patterns

---

Event thiếu version  
Payload không rõ nghĩa  
Dùng event như command  
Không có correlationId  
Shared event schema giữa service

---

---

## 19. Design Guarantees

---

Event Catalog đảm bảo:

- Event rõ ràng, dễ hiểu
- Service decoupled
- Hệ thống scale tốt
- Debug dễ dàng