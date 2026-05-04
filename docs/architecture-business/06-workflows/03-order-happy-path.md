## 1. Mục tiêu

Tài liệu mô tả:

- Luồng nghiệp vụ chính (happy path)
- Tương tác giữa client, services, Kafka
- Event timeline end-to-end
- Vai trò của từng thành phần trong hệ thống

---

## 2. Tổng quan luồng

Customer → Create Order  
        → Dispatch (Routing + Optimization)  
        → Driver thực hiện  
        → Tracking realtime  
        → Payment  
        → Completed

---

## 3. Kiến trúc luồng (Event-driven với Kafka)

Client  
  ↓  
API Gateway (YARP)  
  ↓  
Order Service (.NET)  
  ↓  
Kafka  ← backbone  
  ↓  
Dispatch / Routing / Optimization  
  ↓  
Fleet / Tracking  
  ↓  
Payment

---

## 4. Step-by-step Flow (Happy Path)

---

### 4.1 Create Order

---

#### Flow

Customer App (NextJS / Flutter)  
  ↓  
API Gateway  
  ↓  
Order Service

---

#### Xử lý

- Validate request
- Tạo Order (status = CREATED)
- Lưu MySQL (write DB)
- Ghi Outbox

---

#### Event

OrderCreated

→ publish qua Apache Kafka

---

---

### 4.2 Dispatch bắt đầu (async qua Kafka)

---

#### Trigger

OrderCreated (Kafka topic: order.events)

---

---

### 4.3 Routing Service (Rust)

---

#### Consume

OrderCreated

---

#### Xử lý

- Query dữ liệu từ PostGIS
- Sử dụng OpenStreetMap
- Tính distance + ETA

---

#### Emit

RouteCalculated

→ Kafka: routing.events

---

---

### 4.4 Optimization Service (Python)

---

#### Consume

OrderCreated / RouteCalculated

---

#### Tech

- Google OR-Tools

---

#### Xử lý

- Lấy danh sách driver available
- Solve bài toán assignment

---

#### Emit

DriverAssigned

→ Kafka: dispatch.events

---

---

### 4.5 Fleet Service

---

#### Consume

DriverAssigned

---

#### Xử lý

- Update driver → BUSY
- Update vehicle → IN_USE
- Persist MySQL

---

---

### 4.6 Order Service cập nhật trạng thái

---

#### Consume

DriverAssigned

---

#### Update

CREATED → ASSIGNED

---

#### Emit

OrderAssigned

---

---

### 4.7 Tracking bắt đầu

---

#### Trigger

OrderAssigned

---

#### Tracking Service

- Tạo tracking session
- Lưu MongoDB

---

---

### Driver App

- Gửi location mỗi 1–5s

---

---

### Data flow

Driver App  
  ↓  
Tracking Service  
  ↓  
MongoDB  
  ↓  
SignalR → Customer App

---

---

### 4.8 Pickup

---

#### Driver action

POST /orders/{id}/pickup

---

#### Flow

Driver App → Gateway → Order Service

---

#### Update

ASSIGNED → PICKED_UP

---

#### Emit

OrderPickedUp

→ Kafka

---

---

### 4.9 In Transit

---

- Tracking stream liên tục
- UI update realtime

---

---

### 4.10 Delivery

---

#### Driver action

POST /orders/{id}/deliver

---

#### Update

IN_TRANSIT → DELIVERED

---

#### Emit

OrderDelivered

→ Kafka

---

---

### 4.11 Payment Flow

---

#### Trigger

OrderDelivered

---

#### Payment Service

- Create payment
- Call external gateway

---

---

#### Emit

PaymentCompleted

→ Kafka

---

---

### 4.12 Complete Order

---

#### Order Service

---

#### Consume

PaymentCompleted

---

#### Update

DELIVERED → COMPLETED

---

---

## 5. Notification Flow

---

#### Trigger events

OrderAssigned  
OrderPickedUp  
OrderDelivered  
PaymentCompleted

---

#### Notification Service

- Push notification
- Email / SMS

---

---

## 6. Event Timeline (End-to-End)

---

OrderCreated  
 → RouteCalculated  
 → DriverAssigned  
 → OrderAssigned  
 → OrderPickedUp  
 → LocationUpdated*  
 → OrderDelivered  
 → PaymentCompleted  
 → OrderCompleted

---

---

## 7. Vai trò của Kafka (Core Insight)

---

### Kafka là backbone của hệ thống

---

#### 1. Decoupling

Order không cần biết Dispatch

---

---

#### 2. Buffer

Handle traffic spike

---

---

#### 3. Replay

Có thể reprocess event

---

---

#### 4. Scalability

Scale consumer độc lập

---

---

## 8. Async Boundaries

---

OrderCreated → Kafka → Dispatch  
DriverAssigned → Kafka → Order / Fleet  
OrderDelivered → Kafka → Payment

---

→ Tất cả đều:

Eventually consistent

---

---

## 9. Observability xuyên suốt

---

- Metrics: Prometheus
- Logs: Grafana Loki
- Traces: OpenTelemetry → Grafana Tempo
- Dashboard: Grafana

---

---

## 10. Design Guarantees

---

Flow này đảm bảo:

- Không có synchronous chain dài
- Service độc lập
- Scale từng phần
- Recover được khi fail