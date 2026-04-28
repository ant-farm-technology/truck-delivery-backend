# Bounded Contexts — Ownership & Anti-patterns

## Context Classification (DDD)

| Context | Type | Service | Port |
|---|---|---|---|
| Dispatch | **Core Domain** | Shipment Service | :8086 |
| Order | Supporting | Order Service | :8082 |
| Fleet | Supporting | Driver Service | :8083 |
| Tracking | Supporting | Tracking Service | :8087 |
| Payment | Supporting | Payment Service | :8089 |
| Identity | Generic | Identity Service | :8081 |
| Routing | Generic | Route Service (Rust) | :8084 |
| Optimization | Generic | Optimizer (Python) | :8085 |
| Notification | Supporting | Notification Service | :8088 |

## Context Map (Integration Patterns)

```
Order ──event──▶ Shipment ──sync──▶ Route Service
                    │     ──sync──▶ Optimizer
                    │     ──event──▶ Fleet (DriverAssigned)
                    │
                    └──event──▶ Order (status update)

Tracking (event-driven, parallel)
Identity (JWT, global, cross-cutting)
Payment ──triggered by──▶ OrderDelivered event
Notification ──reacts to──▶ all status events
```

## Identity Context

**Owns:** User, Credential, Role, RefreshToken  
**API:** `/api/v1/auth/*`  
**Publishes:** `UserRegisteredEvent` → topic `userregistered`

**KHÔNG làm:**
- Không chứa Driver profile
- Không chứa Order data
- Không gọi trực tiếp service khác

## Order Context

**Owns:** Order, OrderItem  
**State Machine:** `Pending → Confirmed → AssignedToDriver → PickedUp → InTransit → Delivered → Cancelled`  
**API:** `/api/v1/orders`  
**Publishes:** `OrderCreatedEvent` → topic `order.order.created`  
**Consumes:** `DriverAssignedEvent` (update status)

**KHÔNG làm:**
- Không assign driver trực tiếp
- Không gọi Optimizer
- Không chứa route info

## Fleet Context (Driver & Vehicle)

**Owns:** Driver, Vehicle, DriverVehicleAssignment  
**Driver States:** `Offline → Idle → Busy → Idle`  
**Vehicle States:** `Available → InUse → Maintenance`  
**API:** `/api/v1/drivers`, `/api/v1/vehicles`  
**Publishes:** `DriverAvailable`, `DriverBusy`, `VehicleAssigned`  
**Consumes:** `UserRegisteredEvent` (tạo Driver profile nếu Role=Driver)

**KHÔNG làm:**
- Không assign order
- Không quyết định route
- Không tối ưu tuyến

**Race condition:** 2 Dispatch cùng assign 1 driver → dùng optimistic locking

## Dispatch Context (Shipment Service) — CORE

**Owns:** Shipment, SagaState (MongoDB)  
**ShipmentStatus:** `Created → RoutePlanning → DriverAssigning → DriverConfirmed → InProgress → Completed → Failed`  
**Publishes:** `DispatchRequested`, `RouteRequested`, `DriverAssignedEvent`, `DispatchCompleted`  
**Consumes:** `OrderCreatedEvent`, `DriverAvailable`, `RouteGenerated`, `OptimizationCompleted`

**Saga Steps:**
1. `OrderCreatedEvent` → tạo Shipment
2. Gọi Route Service (Rust) → lấy tuyến đường
3. Gọi Optimizer (Python) → chọn tài xế
4. Publish `DriverAssignmentRequestedEvent`
5. Driver Service confirm → `DriverAssignedEvent`
6. Publish `ShipmentStartedEvent`

**KHÔNG làm:**
- Không tự tính route (delegated sang Rust service)
- Không solve VRP (delegated sang Python service)
- Không lưu raw GPS

**Batching Strategy:** Hybrid — trigger khi đủ N orders HOẶC timeout (30s–2min)

## Routing Context (Rust Route Service)

**Technology:** Rust + axum + tokio + sqlx + PostGIS  
**Owns:** PostGIS road network (roads, nodes, edges)  
**API:** `GET /route`, `GET /matrix`, `GET /nearby-drivers`  
**Algorithm:** A* shortest path, Haversine fallback

**KHÔNG làm:**
- Không chứa business logic
- Không truy cập MySQL
- Không quyết định driver assignment

**Determinism:** Same input → same output (no random without fixed seed)

## Optimization Context (Python Optimizer)

**Technology:** Python + FastAPI + OR-Tools (VRP solver)  
**Owns:** Nothing — stateless compute  
**API:** `POST /optimize`  
**Problems:** CVRP (capacity), VRPTW (time window)  
**Cost:** `Distance × W1 + Time × W2 + Penalty × W3`

**KHÔNG làm:**
- Không truy cập DB
- Không lưu state
- Không chứa business rule

**Timeout:** 5–10s max, return best solution so far  
**Fallback:** Greedy assignment (nearest driver)

## Tracking Context

**Technology:** .NET 10 + SignalR + MongoDB  
**Owns:** TrackingPoint (MongoDB), TrackingSession  
**API:** `POST /tracking/location` (1–5s interval per driver)  
**SignalR:** `/hubs/tracking`, channels: `tracking:{orderId}`, `tracking:{driverId}`  
**Publishes:** `LocationUpdated`  
**Consumes:** `DriverAssigned` (start session), `OrderCompleted` (end session)

**KHÔNG làm:**
- Không thay đổi order state
- Không tính route
- Không assign driver

**Scale:** 10k drivers = ~50k events/sec → horizontal scaling + backpressure

## Payment Context

**Technology:** .NET 10  
**Owns:** Payment, Transaction, IdempotencyKey (MySQL)  
**State Machine:** `Created → Pending → Authorized → Captured → Completed (/ Failed → Refunded)`  
**Publishes:** `PaymentCompleted`, `PaymentFailed`, `RefundIssued`  
**Consumes:** `OrderDelivered`, `OrderCancelled`

**Fare Formula:**
```
TotalFee = BaseFee(VehicleType) + DistanceKm × RatePerKm(VehicleType) + WeightSurcharge
```

**KHÔNG làm:**
- Không xử lý order lifecycle
- Không track GPS
- Trust gateway 100% (phải có reconciliation job)

## Notification Context

**Technology:** .NET 10  
**Channels:** Push (Firebase FCM), SMS (Twilio), Email (SMTP)  
**Consumes:** `DriverAssigned`, `OrderPickedUp`, `OrderDelivered`, `PaymentCompleted`  
**Channel selection:** Critical → SMS + Push, Realtime → Push, Low priority → Email

**KHÔNG làm:**
- Không chứa business logic
- Không quyết định khi nào gửi (chỉ react event)
- Không quản lý order/dispatch
