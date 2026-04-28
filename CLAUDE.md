# Truck Delivery Backend — Claude Rules

## Project Overview
Hệ thống giao hàng xe tải (truck delivery). Backend-only, greenfield, polyglot.

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
- Message broker: Apache Kafka
- Cache: Redis
- Logging: Serilog → Grafana Loki
- Monitoring: Prometheus
- Tracing: OpenTelemetry + Grafana Tempo
- Dashboard: Grafana
- Realtime: SignalR (WebSocket)

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

### API Endpoints
- RESTful, versioned: `/api/v1/{resource}`
- Luôn có `X-Correlation-Id` header propagation

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
- Prometheus metrics endpoint
- Kafka consumer group với idempotency check
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

---

## AI Generation Rules
- Khi generate Entity: phải có private constructor + static factory method
- Khi generate Kafka consumer: luôn có idempotency check bằng MessageId
- Khi generate API endpoint: luôn có correlation-id header propagation
- Khi generate Rust service: dùng `tokio` async runtime
- Khi generate Python service: dùng FastAPI + async/await
- Mọi service mới phải có `/health` và `/ready` endpoint
- Khi generate Saga step: luôn có compensating transaction event
