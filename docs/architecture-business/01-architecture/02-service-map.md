## 1. Mục tiêu

Tài liệu này định nghĩa:

- Ranh giới giữa các service
- Quyền sở hữu dữ liệu (data ownership)
- Cách các service giao tiếp với nhau

---

## 2. Service Classification

Hệ thống chia thành 3 nhóm:

1. Core Business Services (.NET)  
2. Compute Services (Rust / Python)  
3. Supporting Services (infra, cross-cutting)

---

## 3. Service Map Overview

API Gateway  
   ↓  
-------------------------------------  
| Core Services (.NET)              |  
-------------------------------------  
| Identity | Order | Driver         |  
| Shipment | Tracking | Payment     |  
-------------------------------------  
          ↓ (Kafka)  
-------------------------------------  
| Compute Services                 |  
-------------------------------------  
| Route (Rust) | Optimizer (Python)|  
-------------------------------------

---

## 4. Core Services (Business Logic)

---

### 4.1 Identity Service

#### Responsibility

- Authentication (JWT)
- User management
- Role/permission

#### Owns Data

- Users
- Credentials

#### API Surface

POST /auth/login  
POST /auth/register  
GET  /users/{id}

#### Emits Events

UserRegistered  
UserRoleAssigned

#### Anti-patterns

Không validate business rules (Order, Driver)  
Không chứa profile logic (Driver)

---

### 4.2 Order Service

#### Responsibility

- Quản lý đơn hàng
- State machine của Order

#### Owns Data

- Orders (MySQL)

#### API

POST /orders  
GET  /orders/{id}  
PATCH /orders/{id}/status

#### Emits Events

OrderCreated  
OrderCancelled  
OrderReadyForDispatch

#### Consumes

DriverAssigned

#### Anti-patterns

Không assign driver trực tiếp  
Không gọi optimizer

### 4.3 Driver Service

#### Responsibility

- Quản lý tài xế
- Trạng thái availability

#### Owns Data

- Drivers
- DriverVehicle mapping

#### API

POST /drivers  
POST /drivers/{id}/assign-vehicle  
PATCH /drivers/{id}/status

#### Emits Events

DriverAvailable  
DriverBusy

#### Anti-patterns

Không quyết định route  
Không xử lý đơn hàng

---

### 4.4 Shipment Service (Dispatch Core)

**Core domain của toàn hệ thống**

---

#### Responsibility

- Điều phối giao hàng
- Orchestrate routing + optimization
- Assign driver

---

#### Owns Data

- Shipment / DispatchPlan
- Saga state

---

#### API

POST /shipments/dispatch  
GET  /shipments/{id}

---

#### Emits Events

DispatchRequested  
RouteRequested  
OptimizationRequested  
DriverAssigned  
DispatchCompleted

---

#### Consumes

OrderCreated  
DriverAvailable  
RouteGenerated  
OptimizationCompleted

---

#### Anti-patterns (cực kỳ quan trọng)

Không tự tính route  
Không chứa logic geo  
Không lưu raw GPS

---

### 4.5 Tracking Service

#### Responsibility

- Nhận GPS từ driver
- Broadcast realtime

#### Owns Data

- Tracking logs (MongoDB)

#### API

POST /tracking/location

#### Emits

LocationUpdated

---

#### Tech

- SignalR

---

#### Anti-patterns

Không lưu business state  
Không tính route

---

### 4.6 Notification Service

#### Responsibility

- Gửi notification (SMS, push)

#### Consumes

OrderAssigned  
DeliveryCompleted

---

### 4.7 Payment Service

#### Responsibility

- Tính phí
- Xử lý thanh toán

#### Emits

PaymentCompleted  
PaymentFailed

---

## 5. Compute Services

---

### 5.1 Route Service (Rust)

#### Responsibility

- Distance matrix
- Shortest path
- Map matching

#### Data Source

- OpenStreetMap
- PostGIS

---

#### API

POST /route/calculate  
POST /route/matrix

---

#### Anti-patterns

Không chứa business logic  
Không truy cập MySQL

---

---

### 5.2 Optimization Service (Python)

#### Responsibility

- Solve Vehicle Routing Problem

#### Tool

- Google OR-Tools

---

#### API

POST /optimize

---

#### Input

- Orders  
- Vehicles  
- Distance matrix  
- Constraints

---

#### Output

- Routes  
- Cost

---

#### Anti-patterns

Không truy cập DB  
Không chứa state

---

## 6. Interaction Patterns

---

### 6.1 Sync (HTTP/gRPC)

- Gateway → Services
- Shipment → Route / Optimizer (request-response)

---

### 6.2 Async (Kafka)

OrderCreated  
   ↓  
Shipment  
   ↓  
RouteRequested  
   ↓  
OptimizationRequested  
   ↓  
DriverAssigned

---

## 7. Data Ownership Matrix

|Service|Database|
|---|---|
|Identity|MySQL|
|Order|MySQL|
|Driver|MySQL|
|Shipment|MySQL + MongoDB|
|Tracking|MongoDB|
|Route|PostGIS|
|Optimizer|None|
|Payment|MySQL|

---

## 8. Cross-Service Rules (BẮT BUỘC)

1. Không join DB giữa services  
2. Không gọi sync chain quá 2 hops  
3. Event phải immutable  
4. Mọi service phải idempotent  
5. Compute services phải stateless

---

## 9. Common Failure Boundaries

|Service|Failure Impact|
|---|---|
|Optimizer|Không assign được driver|
|Route|Không tính được đường|
|Tracking|Mất realtime|
|Payment|Không hoàn tất đơn|

---

## 10. Design Guarantees

Hệ thống đảm bảo:

- Không service nào là single point of failure
- Có thể scale độc lập từng service
- Có thể thay thế optimizer mà không ảnh hưởng business