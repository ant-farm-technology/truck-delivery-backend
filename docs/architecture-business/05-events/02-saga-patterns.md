## 1. Mục tiêu

Saga Pattern dùng để:

- Điều phối workflow nhiều service
- Thay thế distributed transaction (2PC)
- Đảm bảo **eventual consistency**

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Saga = Chuỗi bước + Compensate

Step → Step → Step  
  ↓  
Fail → rollback bằng compensate

---

### 2.2 Không có global transaction

Mỗi service tự commit DB của nó

---

### 2.3 Idempotent mọi bước

Retry = không phá state

---

---

## 3. 2 loại Saga

---

### 3.1 Choreography (event-driven)

Service tự react event

---

### 3.2 Orchestration (central coordinator)

1 service điều phối toàn bộ flow

---

---

## 4. Saga trong hệ thống truck delivery

---

|Flow|Type|
|---|---|
|Shipment / Dispatch|Choreography|
|Payment|Orchestration|

---

---

## 5. Shipment Saga (Choreography)

---

### Flow chính

OrderCreated  
   ↓  
Routing Service → calculate route  
   ↓  
Optimization → assign driver  
   ↓  
Fleet → mark driver busy  
   ↓  
Order → status Assigned

---

---

### Event sequence

OrderCreated  
 → RouteCalculated  
 → OptimizationCompleted  
 → DriverAssigned  
 → OrderAssigned

---

---

### Ưu điểm

- Loose coupling  
- Scale tốt

---

### Nhược điểm

- Debug khó  
- Flow phân tán

---

---

## 6. Payment Saga (Orchestration)

---

### Flow

OrderDelivered  
   ↓  
Payment Service (orchestrator)  
   ↓  
Create Payment  
   ↓  
Call Gateway  
   ↓  
Update status  
   ↓  
Emit PaymentCompleted

---

---

### Orchestrator giữ state

PaymentSagaState  
 ├── Step  
 ├── Status  
 ├── RetryCount

---

---

### Ưu điểm

- Dễ control  
- Dễ debug

---

### Nhược điểm

- Centralized  
- Có thể bottleneck

---

---

## 7. Compensation Strategy

---

### Ví dụ Shipment fail

DriverAssigned  
   ↓  
Fleet update fail  
   ↓  
Compensate:  
   - release driver  
   - mark order unassigned

---

---

### Rule

Mỗi step phải có compensate tương ứng

---

---

## 8. Saga State Storage

---

### Options

- MongoDB (recommended)  
- Redis (ephemeral)

---

---

### Schema

{  
  "sagaId": "uuid",  
  "type": "ShipmentSaga",  
  "state": "...",  
  "updatedAt": "..."  
}

---

---

## 9. Idempotency trong Saga

---

### Problem

Event duplicate → step chạy lại

---

### Solution

- Check step status  
- Ignore nếu đã done

---

---

## 10. Timeout Handling

---

### Case

Routing service không trả về

---

### Strategy

- Timeout (5–10s)  
- Retry  
- Fallback

---

---

## 11. Retry Strategy

---

- Exponential backoff  
- Max retry count

---

---

## 12. Ordering & Race Condition

---

### Case

DriverAssigned đến trước OptimizationCompleted

---

### Solution

- Check state trước khi apply

---

---

## 13. Observability

---

### Log

- sagaId  
- step  
- status

---

---

### Trace

correlationId xuyên suốt saga

---

---

## 14. Testing Strategy

---

### Unit

Test từng step

---

### Integration

Test full flow event-driven

---

---

## 15. Anti-patterns

---

Saga không có compensate  
Không lưu state  
Không idempotent  
Retry vô hạn  
Mix choreography + orchestration lung tung

---

---

## 16. Design Guidelines

---

### Khi dùng Choreography

- Flow đơn giản  
- Nhiều service

---

### Khi dùng Orchestration

- Flow phức tạp  
- Cần kiểm soát chặt

---

---

## 17. Design Guarantees

---

Saga Pattern đảm bảo:

- Không cần distributed transaction
- System vẫn consistent
- Có thể recover khi fail