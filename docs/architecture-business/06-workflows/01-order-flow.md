## 1. Mục tiêu

Order Flow mô tả:

- Lifecycle của một order
- Event sequence xuyên suốt hệ thống
- Interaction giữa các context
- Failure & compensation paths

---

## 2. High-level Flow

Create Order  
   ↓  
Dispatch (assign driver)  
   ↓  
Pickup  
   ↓  
Delivery  
   ↓  
Payment  
   ↓  
Completed

---

## 3. Order State Machine

CREATED  
  ↓  
ASSIGNING  
  ↓  
ASSIGNED  
  ↓  
PICKED_UP  
  ↓  
IN_TRANSIT  
  ↓  
DELIVERED  
  ↓  
COMPLETED

---

### Failure branches

ASSIGNING → FAILED_ASSIGNMENT  
IN_TRANSIT → FAILED_DELIVERY  
DELIVERED → PAYMENT_FAILED

---

## 4. Step-by-step Flow (Happy Path)

---

### 4.1 Create Order

Client → API Gateway → Order Service

---

#### Command

CreateOrderCommand

---

#### Events

OrderCreated

---

#### Side effects

- Lưu DB (MySQL)
- Publish event (Kafka qua Outbox)

---

---

### 4.2 Dispatch Saga bắt đầu

---

#### Trigger

OrderCreated

---

#### Actions

1. Gọi Routing → tính tuyến
2. Gọi Optimization → chọn driver

---

#### Events

RouteCalculated  
OptimizationCompleted  
DriverAssigned

---

---

### 4.3 Fleet cập nhật trạng thái

---

#### Consume

DriverAssigned

---

#### Actions

- Driver → BUSY
- Vehicle → IN_USE

---

#### Event

DriverBusy

---

---

### 4.4 Order được assign

---

#### Consume

DriverAssigned

---

#### Update

Order → ASSIGNED

---

#### Event

OrderAssigned

---

---

### 4.5 Tracking bắt đầu

---

#### Trigger

OrderAssigned

---

#### Actions

- Tạo TrackingSession
- Subscribe realtime

---

---

### 4.6 Pickup

---

#### Driver App

POST /orders/{id}/pickup

---

#### Event

OrderPickedUp

---

#### State

ASSIGNED → PICKED_UP

---

---

### 4.7 In Transit

---

#### Tracking

LocationUpdated (stream)

---

#### Broadcast

- SignalR realtime

---

---

### 4.8 Delivery

---

#### Driver action

POST /orders/{id}/deliver

---

#### Event

OrderDelivered

---

#### State

IN_TRANSIT → DELIVERED

---

---

### 4.9 Payment Saga

---

#### Trigger

OrderDelivered

---

#### Flow

Create Payment  
   ↓  
Call gateway  
   ↓  
PaymentCompleted

---

---

### 4.10 Complete Order

---

#### Consume

PaymentCompleted

---

#### Update

Order → COMPLETED

---

---

## 5. Failure Scenarios

---

### 5.1 Dispatch fail

No driver available

---

#### Flow

OrderCreated  
   ↓  
Optimization fail  
   ↓  
Order → FAILED_ASSIGNMENT

---

#### Compensation

- Retry dispatch (delayed)

---

---

### 5.2 Driver cancel

---

DriverAssigned  
   ↓  
DriverCancel  
   ↓  
Re-dispatch

---

---

### 5.3 Delivery fail

---

IN_TRANSIT  
   ↓  
FAILED_DELIVERY

---

#### Compensation

- Re-assign driver
- Notify customer

---

---

### 5.4 Payment fail

---

DELIVERED  
   ↓  
PaymentFailed

---

#### Strategy

- Retry charge
- Manual intervention

---

---

## 6. Event Timeline (End-to-End)

---

OrderCreated  
 → RouteCalculated  
 → OptimizationCompleted  
 → DriverAssigned  
 → OrderAssigned  
 → OrderPickedUp  
 → LocationUpdated*  
 → OrderDelivered  
 → PaymentCreated  
 → PaymentCompleted  
 → OrderCompleted

---

---

## 7. 📡 Cross-context Interaction

---

|Step|Context|
|---|---|
|Create|Order|
|Assign|Dispatch + Fleet|
|Route|Routing|
|Optimize|Optimization|
|Track|Tracking|
|Pay|Payment|
|Notify|Notification|

---

---

## 8. Idempotency Points

---

- CreateOrder (API)  
- DriverAssigned (event)  
- Payment (critical)  
- Notification (anti-spam)

---

---

## 9. Consistency Model

---

Eventually consistent

---

### Example

DriverAssigned → OrderAssigned có delay

---

---

## 10. Observability

---

### CorrelationId xuyên suốt

CreateOrder → PaymentCompleted

---

### Metrics

- Time to assign driver  
- Delivery time  
- Payment success rate

---

---

## 11. Anti-patterns

---

Sync call giữa service cho toàn flow  
Không retry dispatch  
Không handle driver cancel  
Payment blocking order flow  
Không có correlationId

---

---

## 12. Design Guarantees

---

Order Flow đảm bảo:

- Flow rõ ràng
- Có thể recover khi fail
- Không phụ thuộc synchronous chain
- Scale theo từng context