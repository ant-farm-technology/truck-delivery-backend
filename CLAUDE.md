# Truck Delivery Backend — Claude Rules

## Project Overview
Hệ thống giao hàng xe tải (truck delivery). Backend-only, greenfield, polyglot.
Solution file: `TruckDelivery.slnx` (16 .NET projects + 1 Rust crate).

## Languages & Responsibilities
- **.NET 10 (C#)** — primary, business logic, all microservices
- **Rust** — spatial queries (PostGIS), OpenStreetMap data processing
- **Python** — routing optimization (Google OR-Tools / VRP), ML tasks

## Databases
- **MySQL** — relational write DB (CQRS write side, via EFCore)
- **MongoDB** — document store (read projections, NoSQL use cases, Saga state)
- **PostGIS** — spatial/geo data (accessed only from Rust service)

## Infrastructure
- API Gateway: YARP
- Message broker: Apache Kafka (KRaft mode, 3 partitions/topic)
- Cache: Redis
- Logging: Serilog → Grafana Loki
- Monitoring: Prometheus
- Tracing: OpenTelemetry OTLP → Grafana Tempo
- Dashboard: Grafana
- Realtime: SignalR (WebSocket) — Tracking service (`/hubs/tracking`)

---

## Implementation Status

| Service | Technology | Port | Status | DB |
|---|---|---|---|---|
| API Gateway | .NET 10 / YARP | :8080 | ✅ Done | — |
| Identity | .NET 10 | :8081 | ✅ Done | MySQL (`truck_identity`) |
| Order | .NET 10 | :8082 | ✅ Done | MySQL (`truck_order`) |
| Driver/Vehicle | .NET 10 | :8083 | ✅ Done | MySQL (`truck_driver`) |
| Route | Rust / axum | :8084 | ✅ Done | PostGIS |
| Optimizer | Python / FastAPI | :8085 | ✅ Done | None |
| Shipment | .NET 10 | :8086 | ✅ Done | MySQL (`truck_shipment`) + MongoDB |
| Tracking | .NET 10 + SignalR | :8087 | ✅ Done | MongoDB (`truck_tracking`) |
| Notification | .NET 10 | :8088 | ✅ Done | MySQL (`truck_notification`) |
| Payment | .NET 10 | :8089 | ✅ Done | MySQL (`truck_payment`) |

### What's Done in Completed Services
- **Identity:** RegisterUser, Login, RefreshToken commands; JWT service; EFCore User aggregate; `UserRegisteredEvent` → Kafka topic `userregistered`
- **Driver:** RegisterDriver, RegisterVehicle, AssignVehicleToDriver, UpdateDriverStatus commands; GetDriverById, GetVehicleById, ListAvailableDrivers queries; `UserRegisteredConsumer` (consumes `userregistered`); publishes `DriverRegisteredEvent`, `DriverStatusChangedEvent`, `VehicleAssignedToDriverEvent`
- **Order:** CreateOrder, CancelOrder commands; GetOrderById, ListOrdersByCustomer queries; publishes `OrderCreatedEvent` → topic `order.order.created`, `OrderCancelledEvent`
- **Route (Rust):** A\* pathfinding, Haversine fallback; `/route`, `/matrix`, `/nearby-drivers`, `/drivers/:id/location` endpoints; PostGIS migrations (`driver_locations`, `road_network` tables); Redis cache (route 30 min, matrix 15 min, nearby 1 min); OpenTelemetry → Tempo; `setup.ps1/sh` + `run.ps1/sh`
- **Optimizer (Python):** OR-Tools VRP solver (CVRP + optional VRPTW); greedy nearest-driver fallback; `POST /optimize`; Prometheus metrics; OpenTelemetry; `setup.ps1/sh` + `run.ps1/sh`; unit + integration tests
- **Shipment:** `OrderCreatedConsumer` → `CreateShipmentCommand` (Outbox); `DispatchSagaOrchestrator` (polls Created/RoutePlanning → calls Route/Optimizer → publishes `DriverAssignmentRequestedEvent`); `DriverAssignedConsumer` → transitions to InProgress, publishes `ShipmentStartedEvent`; MongoDB saga state; EFCore MySQL write + Dapper read
- **Tracking:** `ShipmentStartedConsumer` → `StartTrackingCommand`; `ShipmentCompletedConsumer` → `StopTrackingCommand`; `POST /api/v1/tracking/location` (driver GPS push); `GET /api/v1/tracking/shipments/{id}/points`; SignalR hub at `/hubs/tracking` (groups by shipment/driver); MongoDB persistence; Mongo Outbox processor
- **Notification:** Consumes `ShipmentStatusUpdatedEvent`, `DriverAssignedEvent`, `PaymentCompletedEvent`; `SendNotificationCommand` → Push/SMS/Email stub senders; EFCore MySQL `NotificationRecord` persistence; Outbox pattern; all consumers use `ConsumerConfig` + own `IConsumer` (thread-safe)
- **Payment:** `OrderDeliveredConsumer` → `CreatePaymentCommand` (COD flow: auto-complete); publishes `PaymentCompletedEvent` via Outbox; `GET /api/v1/payments/orders/{orderId}`; EFCore MySQL + Dapper read; `PaymentStatus` state machine

### Remaining Gaps
- **No test projects** — no unit, integration, or contract tests exist yet.
- **GitHub Actions missing** — `.github/` directory exists but empty.
- **Notification senders are stubs** — StubPushSender/StubSmsSender/StubEmailSender log only; real impl needs FCM/Twilio/SMTP.
- **Payment gateway not wired** — COD flow auto-completes; real gateway integration needed for card/VNPay.

---

## Shared Projects (Already Exists — Reuse, Don't Recreate)

### `TruckDelivery.Shared.Common`
Path: `src/Shared/TruckDelivery.Shared.Common/`

| Class | Purpose |
|---|---|
| `Domain/AggregateRoot.cs` | Base class for all aggregate roots |
| `Domain/Entity.cs` | Base entity class |
| `Domain/IDomainEvent.cs` | Domain event marker interface |
| `Domain/ValueObject.cs` | Base value object |
| `Exceptions/DomainException.cs` | Domain-level exceptions |
| `Exceptions/NotFoundException.cs` | Not found exception |
| `Persistence/IUnitOfWork.cs` | UoW interface |
| `Primitives/Result.cs` | Result<T> pattern |
| `Primitives/Guard.cs` | Guard clause helpers |
| `Primitives/Error.cs` | Error type |

### `TruckDelivery.Shared.Contracts`
Path: `src/Shared/TruckDelivery.Shared.Contracts/`

| Class | Purpose |
|---|---|
| `Events/IntegrationEvent.cs` | Base Kafka event (has `MessageId`, `OccurredAt`, `SchemaVersion`) |
| `Events/UserRegisteredEvent.cs` | Topic: `userregistered` |

### `TruckDelivery.Shared.Infrastructure`
Path: `src/Shared/TruckDelivery.Shared.Infrastructure/`

| Class | Purpose |
|---|---|
| `Messaging/IEventBus.cs` | Event publishing interface |
| `Messaging/Kafka/KafkaEventBus.cs` | Kafka producer (injects OTel `traceparent`) |
| `Messaging/Kafka/KafkaConsumerBase.cs` | Base `BackgroundService` for consumers |
| `Messaging/Kafka/Idempotency/IIdempotencyStore.cs` | Idempotency check interface |
| `Messaging/Kafka/Idempotency/RedisIdempotencyStore.cs` | Redis-backed, TTL 24h |
| `Caching/ICacheService.cs` | Cache interface |
| `Caching/Redis/RedisCacheService.cs` | Redis implementation |
| `Persistence/IDbConnectionFactory.cs` | Dapper connection factory interface |
| `Persistence/MySql/MySqlConnectionFactory.cs` | MySQL Dapper connection |
| `Telemetry/TelemetryExtensions.cs` | OpenTelemetry DI setup |
| `Extensions/ServiceCollectionExtensions.cs` | Registers Redis, Kafka, MySQL |

---

## Architecture Laws (non-negotiable)

### Microservices + DDD
- Mỗi service là một Bounded Context độc lập
- Services KHÔNG share database với nhau
- Domain model không bị lộ ra ngoài service

### CQRS (strict split)
- **Command (write):** EFCore → MySQL
- **Query (read):** Dapper → MySQL read replica hoặc MongoDB
- **MongoDriver:** chỉ dùng cho MongoDB collections
- Không được dùng EFCore `.ToList()` / `.FirstOrDefault()` trong Query handlers
- Không được dùng Dapper trong Command handlers

### Event-Driven via Kafka
- Cross-service data sync: publish Kafka event TRƯỚC, không gọi HTTP trực tiếp giữa services
- Mỗi Kafka event phải có `MessageId` (UUID) để idempotency check ở consumer
- Mỗi consumer phải có Dead Letter Queue handler
- Kafka header phải carry OpenTelemetry trace context (`traceparent`)

### Saga Pattern (Choreography-based)
- Distributed transaction dùng Choreography Saga qua Kafka events
- Mỗi step phải có compensating transaction event
- Saga state lưu vào MongoDB

### Mediator (MediatR)
- Mọi Command và Query phải đi qua MediatR handler
- Controller chỉ được gọi `_mediator.Send()` — không chứa business logic

### Repository + UnitOfWork
- Repository chỉ expose aggregate root — không expose entity con trực tiếp
- UnitOfWork wrap transaction ở Application layer, không ở Repository hay Domain layer
- Interface định nghĩa ở Domain layer, implementation ở Infrastructure layer

### Spatial & Routing
- PostGIS queries: viết trong Rust service — không viết trong .NET
- OR-Tools (VRP/routing): viết trong Python service — không viết trong .NET
- .NET services giao tiếp với Rust/Python qua HTTP hoặc gRPC

### Non-blocking
- Async/await everywhere trong .NET — không có blocking call (`.Result`, `.Wait()`)
- Rust: dùng `tokio::spawn` — không dùng `std::thread::spawn` cho I/O
- Python: async endpoints với FastAPI + asyncio

---

## Naming Conventions

### .NET
- Commands: `CreateOrderCommand`, `AssignDriverCommand`
- Queries: `GetOrderByIdQuery`, `ListAvailableDriversQuery`
- Command Results: `CreateOrderResult`
- Domain Events: `OrderCreatedDomainEvent`
- Kafka Events: `OrderCreatedEvent`, `DriverAssignedEvent`
- Handlers: `CreateOrderCommandHandler`, `GetOrderByIdQueryHandler`
- Repositories (interface): `IOrderRepository`
- Aggregates: `Order`, `Driver`, `Shipment`

### Kafka Topics
- Format: `{service}.{entity}.{action}` (lowercase, dots)
- Examples: `order.order.created`, `driver.driver.assigned`, `shipment.shipment.status-updated`
- DLQ: `{topic}.dlq`
- Legacy exception: `userregistered` (Identity → Driver, giữ nguyên)

### API Endpoints
- RESTful, versioned: `/api/v1/{resource}`
- Luôn có `X-Correlation-Id` header propagation

---

## Folder Structure Convention (.NET Service)

```
src/Services/{ServiceName}/
  {ServiceName}.Domain/
    Aggregates/           ← Aggregate roots + entities
    ValueObjects/         ← Records (immutable)
    Events/               ← Domain events (internal)
    Repositories/         ← Interfaces only
    Exceptions/           ← Domain-specific exceptions
  {ServiceName}.Application/
    Commands/             ← Command + Handler + Validator per subfolder
    Queries/              ← Query + Handler per subfolder
    Consumers/            ← Kafka BackgroundService consumers
    IntegrationEvents/    ← Kafka event DTOs published by this service
    DTOs/                 ← Read-side data transfer objects
    Interfaces/           ← IUnitOfWork, IEventBus, etc.
    Behaviors/            ← MediatR pipeline behaviors
  {ServiceName}.Infrastructure/
    Persistence/
      EFCore/             ← DbContext, Configurations/, migrations
      Dapper/             ← Read query repos
      Mongo/              ← MongoDB collections, saga state
      Outbox/             ← OutboxMessage, OutboxProcessor
    Messaging/Kafka/
      Producers/          ← Event publishers
      Consumers/          ← DLQ handlers
    Caching/Redis/        ← Idempotency, cache
    Extensions/           ← ServiceCollectionExtensions.cs
  {ServiceName}.Api/
    Controllers/          ← Thin controllers
    Middlewares/          ← GlobalException, CorrelationId
    Program.cs
    appsettings.json
    Dockerfile
```

---

## Code Conventions

### Comments
- Không comment giải thích WHAT — chỉ comment WHY khi thực sự cần thiết
- Không viết multi-line comment blocks
- Không docstring dài

### Error Handling
- Dùng Result pattern hoặc custom Exception cho domain errors
- Không dùng generic `Exception` ở domain layer
- Global exception middleware ở API layer

### Validation
- FluentValidation cho Command validation ở Application layer
- Không validate ở Controller, không validate ở Domain (domain dùng guard clauses)

---

## Every New Service Must Have
- `/health` endpoint (liveness)
- `/ready` endpoint (readiness)
- OpenTelemetry ActivitySource registered
- Serilog structured logging với correlation-id enricher
- Prometheus metrics endpoint (`/metrics`)
- Kafka consumer group với idempotency check (via `RedisIdempotencyStore`)
- Outbox pattern khi publish Kafka events
- Docker-ready `Dockerfile` với multi-stage build

---

## What NOT to Do
- Không dùng EFCore trong Query handlers
- Không gọi HTTP giữa services trong Domain layer
- Không đặt business logic trong Controller
- Không bỏ qua OpenTelemetry tracing khi tạo service mới
- Không viết spatial logic trong .NET — phải là Rust service
- Không viết OR-Tools solver trong .NET — phải là Python service
- Không share domain models giữa các services (dùng DTOs/contracts qua Kafka events)
- Không dùng `.Result` hay `.Wait()` trong async code
- Không publish Kafka event trực tiếp trong Command handler — phải qua Outbox

---

## AI Generation Rules
- Khi generate Entity: phải có private constructor + static factory method
- Khi generate Kafka consumer: kế thừa `KafkaConsumerBase` từ Shared.Infrastructure; luôn có idempotency check bằng `MessageId`
- Khi generate integration event: kế thừa `IntegrationEvent` từ `TruckDelivery.Shared.Contracts`
- Khi generate API endpoint: luôn có correlation-id header propagation
- Khi generate Rust service: dùng `tokio` async runtime
- Khi generate Python service: dùng FastAPI + async/await
- Mọi service mới phải có `/health` và `/ready` endpoint
- Khi generate Saga step: luôn có compensating transaction event
- Khi generate Command handler: phải dùng Outbox pattern (save OutboxMessage trong cùng transaction với entity)
- Không tạo lại các abstractions đã có trong Shared projects — reuse trực tiếp
