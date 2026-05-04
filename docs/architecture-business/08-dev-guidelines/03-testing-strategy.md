## 1. Mục tiêu

Testing Strategy đảm bảo:

- Phát hiện bug sớm
- Giữ chất lượng code ổn định
- Cho phép refactor an toàn
- Validate flow end-to-end

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Test Pyramid

          E2E  
        /     \  
   Integration  
     /       \  
   Unit Tests

---

### 2.2 Fast > Perfect

Test phải chạy nhanh → CI hiệu quả

---

---

### 2.3 Test behavior, không test implementation

Đúng:               “Order phải chuyển sang ASSIGNED”  
Không đúng:    “Method X được gọi”

---

---

## 3. Test Levels

---

## 3.1 Unit Test (70%)

---

### Scope

- Domain logic
- Value Objects
- Business rules

---

### Example

Order.AssignDriver()  
→ status = ASSIGNED

---

### Rule

- Không DB  
- Không network  
- Không Kafka

---

---

### Tools

- .NET → xUnit
- Rust → built-in test
- Python → pytest

---

---

## 3.2 Integration Test (20%)

---

### Scope

- DB (MySQL, MongoDB)
- Kafka
- Redis

---

---

### Example

Create Order  
→ DB insert  
→ Event publish (Kafka)

---

---

### Strategy

- Testcontainers (Docker)  
- Real infra (lightweight)

---

---

## 3.3 Contract Test (5–10%)

---

### Scope

- API contracts giữa services
- Event schema

---

---

### Example

OrderCreated event  
→ phải đúng schema

---

---

### Tool

- Pact (API)
- JSON schema validation (event)

---

---

## 3.4 End-to-End Test (5%)

---

### Scope

- Full flow:

Create Order  
→ Assign Driver  
→ Delivery  
→ Payment

---

---

### Rule

- Ít nhưng critical  
- Không test mọi case

---

---

## 4. Event-driven Testing

---

### Problem

Async → khó test

---

---

### Solution

- Mock event bus (unit)  
- Real Kafka (integration)

---

---

### Example

Publish OrderCreated  
→ expect DriverAssigned

---

---

## 5. Saga Testing

---

### Scope

- Saga flow
- Compensation

---

---

### Example

Assign driver fail  
→ rollback

---

---

### Strategy

- Test từng step  
- Test full saga flow

---

---

## 6. API Testing

---

### Types

- Happy path  
- Validation error  
- Auth error

---

---

### Example

POST /orders  
→ 201 Created

---

---

## 7. Performance Testing

---

### Tools

- k6
- JMeter

---

---

### Scenarios

- Load test  
- Stress test  
- Spike test  
- Soak test

---

---

### Focus

Tracking service (hot path)  
Kafka throughput

---

---

## 8. Chaos Testing (Advanced)

---

### Purpose

Test system khi fail thật

---

---

### Scenarios

- Kill service  
- Kafka down  
- DB latency cao

---

---

## 9. Test Data Strategy

---

### Rule

- Data độc lập mỗi test  
- Không reuse global state

---

---

### Techniques

- Factory pattern  
- Fixtures

---

---

## 10. CI/CD Integration

---

### Pipeline

Code → Unit → Integration → Build → Deploy

---

---

### Rule

Fail test → block deploy

---

---

## 11. Coverage Strategy

---

### Target

- Domain: 90%+  
- Application: 70%  
- Infra: thấp hơn

---

---

### Note

Coverage cao ≠ test tốt

---

---

## 12. Idempotency Testing

---

### Case

Gửi cùng request 2 lần

---

---

### Expect

Không duplicate order

---

---

## 13. Concurrency Testing

---

### Case

2 request assign cùng driver

---

---

### Expect

Chỉ 1 thành công

---

---

## 14. Failure Testing

---

### Case

Kafka delay  
DB timeout

---

---

### Expect

Retry + no crash

---

---

## 15. Observability Testing

---

### Check

- Log có correlationId  
- Trace đầy đủ

---

---

## 16. Anti-patterns

---

Test quá phụ thuộc implementation  
Không test failure case  
Test chậm → bị bỏ qua  
Không test event-driven flow  
Không có integration test

---

---

## 17. Design Guarantees

---

Testing Strategy đảm bảo:

- Code an toàn khi thay đổi
- System hoạt động đúng
- Giảm bug production