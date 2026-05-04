# Architecture Laws — Non-Negotiable

## Separation of Compute

| Logic type | Where |
|---|---|
| Business logic | .NET services |
| Spatial / geo | Rust (Route Service) + PostGIS |
| Route optimization (VRP) | Python (Optimizer) + OR-Tools |

**Nếu vi phạm rule này → system sẽ choke khi scale.**

## Data Ownership

- **1 service = 1 database** — không shared DB
- Cross-service data sync chỉ qua Kafka events hoặc API
- Không join DB giữa 2 services

| Service | Database |
|---|---|
| Identity | MySQL |
| Order | MySQL |
| Driver/Vehicle | MySQL |
| Shipment | MySQL (write) + MongoDB (saga state, logs) |
| Tracking | MongoDB |
| Route (Rust) | PostGIS |
| Payment | MySQL |
| Notification | MySQL |

## CQRS Strict Split

```
Command handler → EFCore → MySQL (write)
Query handler   → Dapper → MySQL replica hoặc MongoDB (read)
```

- **KHÔNG** dùng EFCore trong Query handlers
- **KHÔNG** dùng Dapper trong Command handlers
- **KHÔNG** dùng `.ToList()` / `.FirstOrDefault()` qua EFCore ở Query handlers

## Event-Driven First

- Cross-service call → **Kafka event trước**, không HTTP trực tiếp
- Mỗi event = Fact (đã xảy ra), không phải Command
- At-least-once delivery → mọi consumer **phải idempotent**

## Saga = Choreography (Shipment), Orchestration (Payment)

| Flow | Pattern |
|---|---|
| Shipment dispatch | Choreography (events react nhau) |
| Payment | Orchestration (Payment Service điều phối) |

## Compute Services = Stateless

- Rust Route Service: không lưu state, không truy cập MySQL
- Python Optimizer: không lưu state, không có DB riêng
- Scale = thêm pod, không cần migration

## Non-blocking Always

- .NET: `async/await` everywhere, **không** `.Result`, `.Wait()`, `Thread.Sleep`
- Rust: `tokio::spawn`, không `std::thread::spawn` cho I/O
- Python: `async def` + `asyncio`, FastAPI async endpoints

## Every New Service Must Have

- `GET /health` — liveness
- `GET /ready` — readiness
- OpenTelemetry ActivitySource
- Serilog structured JSON logging với `correlationId` enricher
- Prometheus `/metrics` endpoint
- Kafka consumer với idempotency check
- Outbox pattern (nếu publish Kafka events)

## Anti-patterns (TUYỆT ĐỐI KHÔNG)

- `DbContext` trong Query handler
- `HttpClient` trong Domain layer
- Business logic trong Controller
- `.Result` hoặc `.Wait()` trong async code
- OR-Tools trong .NET service
- PostGIS / spatial SQL trong .NET
- Share domain Entity giữa 2 services
- Event không có `MessageId` (idempotency key)
- Consumer không có Dead Letter Queue handler
