## 1. Mục tiêu kiến trúc

Hệ thống **Truck Delivery System** được thiết kế để giải quyết bài toán:

> Tối ưu hoá giao hàng quy mô lớn với ràng buộc thực tế (capacity, time window, geo distance, driver availability)

### Yêu cầu cốt lõi:

- High throughput (nhiều đơn hàng đồng thời)
- Compute-heavy (routing + optimization)
- Real-time tracking
- Eventual consistency (không thể strong consistency toàn hệ)

## 2. Architectural Principles

---

### 2.1 Separation of Concerns (bắt buộc)

- Business logic → .NET services  
- Compute-heavy → Rust / Python  
- Geo queries → PostGIS

Không phá vỡ rule này. Nếu phá → system sẽ choke khi scale.

---

### 2.2 Event-Driven First

- Mọi workflow quan trọng đều qua event
- Tránh synchronous chaining giữa services

Lý do:

- Giảm coupling
- Tăng resilience
- Dễ scale

---

### 2.3 Compute Isolation

Các tác vụ nặng:

- Route calculation
- Distance matrix
- Vehicle routing optimization

**không chạy trong .NET service**

---

### 2.4 Data Ownership

1 service = 1 database ownership

- Không shared DB
- Giao tiếp qua API / event

---

### 2.5 Eventually Consistent System

- Chấp nhận delay vài giây
- Đổi lại: scale + resilience

## 3. System Architecture Overview

                [ Client Apps ]  
        (Mobile / Web / Admin)  
                        │  
                        ▼  
           [ API Gateway (.NET - YARP) ]  
                        │  
    ┌──────────────────────────────────────────┐  
    │            Core Services (.NET)          │  
    │------------------------------------------│  
    │ Identity Service                         │  
    │ Order Service                            │  
    │ Driver Service                           │  
    │ Shipment (Dispatch) Service              │  
    │ Tracking Service                         │  
    │ Notification Service                     │  
    │ Payment Service                          │  
    └──────────────────────────────────────────┘  
                        │  
                (Event Bus - Kafka)  
                        │  
    ┌──────────────────────────────────────────┐  
    │           Compute Services               │  
    │------------------------------------------│  
    │ Route Service (Rust)                     │  
    │ Optimization Service (Python)            │  
    └──────────────────────────────────────────┘  
                        │  
    ┌──────────────────────────────────────────┐  
    │              Data Layer                  │  
    │------------------------------------------│  
    │ MySQL (OLTP - source of truth)           │  
    │ MongoDB (projections / tracking)         │  
    │ PostGIS (geo spatial queries)            │  
    │ Redis (cache / idempotency)              │  
    └──────────────────────────────────────────┘

---

## 4. Layer Responsibilities

---

### 4.1 API Gateway

- Routing request
- JWT validation
- Inject `X-Correlation-Id`

Không chứa business logic

---

### 4.2 Core Services (.NET)

Đây là nơi chứa:

- Domain logic (DDD)
- State transitions
- Saga orchestration (Shipment)

---

### 4.3 Compute Services

---

#### Route Service (Rust)

- Tính toán distance
- Map matching
- Graph traversal

Dữ liệu:

- OpenStreetMap
- PostGIS

---

#### Optimization Service (Python)

- Solve VRP
- Handle constraints

Sử dụng:

- Google OR-Tools

---

Quan trọng:

.NET không gọi OR-Tools trực tiếp

---

### 4.4 Data Layer

---

#### MySQL

- Orders
- Vehicles
- Drivers

Source of truth

---

#### MongoDB

- Tracking logs
- Event projections
- Saga state

---

#### PostGIS

- Road network
- Spatial queries

---

#### Redis

- Cache route
- Idempotency key

---

## 5. Communication Patterns

---

### 5.1 Synchronous (ít dùng)

- API Gateway → Services
- Service → lightweight queries

---

### 5.2 Asynchronous (chính)

Qua Kafka:

OrderCreated  
   ↓  
Shipment Service  
   ↓  
RouteRequested  
   ↓  
OptimizationRequested  
   ↓  
RouteGenerated  
   ↓  
DriverAssigned

---

## 6. Deployment Model

---

### Containerized (Docker)

- Mỗi service = 1 container

---

### Orchestration

- Kubernetes

---

### Scaling Strategy

|Component|Scale Strategy|
|---|---|
|API Gateway|Horizontal|
|.NET services|Horizontal|
|Rust service|CPU-bound scaling|
|Python optimizer|Queue-based scaling|

---

## 7. Observability

---

### Logging

- Serilog → Grafana Loki

### Tracing

- OpenTelemetry → Grafana Tempo

### Metrics

- Prometheus → Grafana

---

### Correlation

- Mọi request phải có:

X-Correlation-Id

---

## 8. Security Model

---

- JWT authentication (Gateway)
- Role-based authorization (service level)
- Service-to-service auth (internal token / mTLS)

---

## 9. Architectural Constraints (bắt buộc tuân thủ)

---

1. Không service nào truy cập DB của service khác  
2. Không xử lý thuật toán nặng trong .NET  
3. Mọi workflow phải event-driven  
4. Compute services phải stateless  
5. Routing/Optimization không chứa business logic

---

## 10. Known Trade-offs

---

|Trade-off|Lý do|
|---|---|
|Eventual consistency|Để scale|
|Tách nhiều service|Tăng complexity nhưng giảm coupling|
|Python cho optimizer|Dễ dùng OR-Tools nhưng chậm hơn Rust|

---

## 11. Evolution Strategy

---

### Phase 1 (MVP)

- Order + Shipment + Optimizer
- MySQL only

---

### Phase 2

- Thêm Redis + MongoDB
- Tracking realtime

---

### Phase 3

- Full microservices + scaling
- Advanced optimization