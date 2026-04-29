## 1. Mục tiêu

Payment Context chịu trách nhiệm:

- Tính phí vận chuyển
- Tạo và quản lý giao dịch thanh toán
- Tích hợp với payment gateway bên ngoài
- Đảm bảo **không bị double charge**

---

## 2. Boundary & Responsibility

---

### Thuộc Payment

- Fare calculation (có thể tách riêng nếu phức tạp)  
- Payment transaction lifecycle  
- Idempotency  
- Gateway integration  
- Reconciliation

---

### KHÔNG thuộc Payment

- Không xử lý order lifecycle  
- Không xử lý dispatch  
- Không xử lý tracking

---

## 3. Ubiquitous Language

Payment  
Transaction  
Charge  
Refund  
PaymentStatus  
IdempotencyKey  
Settlement

---

## 4. Aggregate Design

---

### Aggregate Root: `Payment`

Payment  
 ├── PaymentId  
 ├── OrderId  
 ├── Amount  
 ├── Currency  
 ├── Status  
 ├── IdempotencyKey  
 ├── ExternalTransactionId  
 ├── CreatedAt  
 └── UpdatedAt

---

### Supporting Entity: `Transaction`

Transaction  
 ├── TransactionId  
 ├── Type (Charge, Refund)  
 ├── Status  
 ├── GatewayResponse  
 ├── CreatedAt

---

---

## 5. Payment State Machine (CRITICAL)

---

CREATED  
  ↓  
PENDING  
  ↓  
AUTHORIZED  
  ↓  
CAPTURED  
  ↓  
COMPLETED

---

### Failure Branch

PENDING → FAILED  
AUTHORIZED → FAILED  
CAPTURED → REFUND_REQUIRED

---

### State Definitions

|State|Ý nghĩa|
|---|---|
|CREATED|vừa tạo|
|PENDING|đang xử lý|
|AUTHORIZED|giữ tiền (pre-auth)|
|CAPTURED|đã trừ tiền|
|COMPLETED|hoàn tất|

---

---

## 6. Fare Calculation

---

### Input

- Distance (Routing)  
- Time (Tracking)  
- Vehicle type

---

### Formula (example)

Fare = BaseFee + Distance * Rate + Time * Rate

---

### Rule

Fare calculation phải deterministic

---

---

## 7. Idempotency (BẮT BUỘC)

---

### Vấn đề

Retry request → double charge

---

### Giải pháp

IdempotencyKey (client-generated)

---

### Flow

Request → check key  
   ↓  
Nếu tồn tại → return result cũ  
   ↓  
Nếu chưa → process + store

---

### Storage

Redis hoặc DB

---

---

## 8. Payment Gateway Integration

---

### Pattern

Payment Service → Gateway API

---

### Flow

Create Payment  
   ↓  
Call Gateway  
   ↓  
Receive response  
   ↓  
Update state

---

### Webhook Handling

Gateway → webhook → Payment Service

---

Rule:

Webhook phải idempotent

---

---

## 9. Domain Events

---

### Publish

PaymentCreated  
PaymentAuthorized  
PaymentCaptured  
PaymentCompleted  
PaymentFailed  
RefundIssued

---

### Consume

OrderDelivered  
OrderCancelled

---

---

## 10. Payment Flow (End-to-End)

---

OrderDelivered  
   ↓  
PaymentCreated  
   ↓  
Charge Request  
   ↓  
Gateway Response  
   ↓  
PaymentCompleted

---

---

## 11. Refund Handling

---

### Trigger

- Delivery failed  
- Customer complaint

---

### Flow

RefundRequested  
   ↓  
Call Gateway  
   ↓  
RefundCompleted

---

---

## 12. Reconciliation (RẤT QUAN TRỌNG)

---

### Vấn đề

Gateway ≠ hệ thống nội bộ

---

### Strategy

Daily reconciliation job

---

### Compare

- Internal transactions  
- Gateway transactions

---

### Output

Mismatch report

---

---

## 13. Persistence Model (MySQL)

---

### Tables

Payments  
Transactions  
IdempotencyKeys

---

---

## 14. Concurrency & Race Conditions

---

### Case

2 request charge cùng lúc

---

### Solution

- Unique constraint (IdempotencyKey)  
- Transaction lock

---

---

## 15. Failure Handling

---

### Case 1: Gateway timeout

→ retry (idempotent)

---

### Case 2: Webhook missing

→ polling fallback

---

### Case 3: Partial success

→ reconciliation fix

---

---

## 16. Security

---

- Encrypt sensitive data  
- Validate webhook signature  
- Không lưu card info (PCI)

---

---

## 17. Anti-patterns

---

Không dùng idempotency  
Trust gateway 100%  
Không có reconciliation  
Sync blocking call toàn hệ thống  
Lưu card data

---

---

## 18. Design Guarantees

---

Payment Context đảm bảo:

- Không double charge
- Có thể retry an toàn
- Có thể reconcile dữ liệu
- Không phụ thuộc hoàn toàn gateway