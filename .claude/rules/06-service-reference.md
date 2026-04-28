# Service Reference — Quick Lookup

## Service Status & Ports

| Service | Technology | Port | Status | DB |
|---|---|---|---|---|
| API Gateway | .NET 10 / YARP | :8080 | ✅ Done | — |
| Identity | .NET 10 | :8081 | ✅ Done | MySQL |
| Order | .NET 10 | :8082 | ✅ Done | MySQL |
| Driver/Vehicle | .NET 10 | :8083 | ✅ Done | MySQL |
| Route | Rust / axum | :8084 | 🔲 Planned | PostGIS |
| Optimizer | Python / FastAPI | :8085 | 🔲 Planned | None |
| Shipment | .NET 10 | :8086 | 🔲 Planned | MySQL + MongoDB |
| Tracking | .NET 10 + SignalR | :8087 | 🔲 Planned | MongoDB |
| Notification | .NET 10 | :8088 | 🔲 Planned | MySQL |
| Payment | .NET 10 | :8089 | 🔲 Planned | MySQL |

## Infrastructure Ports

| Component | Port | Purpose |
|---|---|---|
| Kafka | :9092 | Message broker |
| Zookeeper | :2181 | Kafka coordination |
| MySQL | :3306 | Write DB |
| MongoDB | :27017 | Read projections, Saga state |
| PostGIS | :5432 | Spatial data (Route service only) |
| Redis | :6379 | Cache, idempotency |
| Prometheus | :9090 | Metrics collection |
| Grafana | :3000 | Dashboards |
| Loki | :3100 | Log aggregation |
| Tempo | :4317 | Trace collection |

## Aggregate Reference

| Service | Aggregate Root | Key Methods |
|---|---|---|
| Identity | `User` | `Create()`, `VerifyPassword()`, `SetRefreshToken()` |
| Order | `Order` | `Create()`, `Cancel()`, `UpdateStatus()` |
| Driver/Vehicle | `Driver` | `Create()`, `UpdateStatus()`, `AssignVehicle()` |
| Driver/Vehicle | `Vehicle` | `Create()`, `AssignDriver()`, `SetMaintenance()` |
| Shipment | `Shipment` | `Create()`, `UpdateStatus()`, `Assign()` |
| Payment | `Payment` | `Create()`, `Authorize()`, `Capture()`, `Fail()` |
| Notification | `Notification` | `Create()`, `Send()`, `Retry()` |

## Domain Event Reference

| Event | Publisher | Consumer(s) |
|---|---|---|
| `OrderCreatedDomainEvent` | Order | Order.Application (→ Kafka) |
| `OrderCancelledDomainEvent` | Order | Order.Application (→ Kafka) |
| `OrderStatusChangedDomainEvent` | Order | Order.Application |
| `DriverRegisteredDomainEvent` | Driver | Driver.Application |
| `DriverStatusChangedDomainEvent` | Driver | Driver.Application (→ Kafka) |
| `VehicleAssignedToDriverDomainEvent` | Driver | Driver.Application (→ Kafka) |

## Integration Event (Kafka) Reference

| Event Class | Topic | Producer | Consumer(s) |
|---|---|---|---|
| `UserRegisteredEvent` | `userregistered` | Identity | Driver |
| `OrderCreatedEvent` | `order.order.created` | Order | Shipment |
| `DriverStatusUpdatedEvent` | `driver.driver.status-updated` | Driver | Shipment |
| `VehicleAssignedEvent` | `driver.vehicle.assigned` | Driver | Shipment |
| `ShipmentStatusUpdatedEvent` | `shipment.shipment.status-updated` | Shipment | Order, Notification |
| `DriverAssignedEvent` | `shipment.driver.assigned` | Shipment | Driver, Tracking, Notification |
| `LocationUpdatedEvent` | `tracking.location.updated` | Tracking | Notification |
| `PaymentCompletedEvent` | `payment.payment.completed` | Payment | Order, Notification |
| `PaymentFailedEvent` | `payment.payment.failed` | Payment | Order, Notification |

## Enums Reference

```csharp
// OrderStatus
Pending = 1, Confirmed = 2, AssignedToDriver = 3, 
PickedUp = 4, InTransit = 5, Delivered = 6, Cancelled = 7

// DriverStatus
Offline = 1, Available = 2, Busy = 3, Suspended = 4

// VehicleType
Motorbike = 1, Van = 2, Truck3T = 3, Truck5T = 4, Truck10T = 5, Truck15T = 6

// VehicleStatus
Available = 1, InUse = 2, Maintenance = 3

// UserRole
Customer = 1, Driver = 2, Admin = 3

// ShipmentStatus
Created, RoutePlanning, DriverAssigning, DriverConfirmed, InProgress, Completed, Failed

// PaymentStatus
Created, Pending, Authorized, Captured, Completed, Failed, Refunded
```

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
    Commands/             ← Command + Handler pairs
    Queries/              ← Query + Handler pairs
    EventHandlers/        ← Kafka event handlers (→ Commands)
    DTOs/                 ← Data transfer objects
    Interfaces/           ← IUnitOfWork, IEventBus, etc.
    Behaviors/            ← MediatR pipeline behaviors
  {ServiceName}.Infrastructure/
    Persistence/
      EFCore/             ← DbContext, migrations, write repositories
      Dapper/             ← Read query repositories
      Mongo/              ← MongoDB collections, saga state
      Outbox/             ← OutboxMessage, OutboxProcessor
    Messaging/Kafka/
      Producers/          ← Event publishers
      Consumers/          ← BackgroundService consumers + DLQ
    Caching/Redis/        ← Idempotency store, cache
  {ServiceName}.Api/
    Controllers/          ← Thin controllers, mediator.Send() only
    Middlewares/          ← Global exception, correlation-id
    Program.cs
    Dockerfile
```

## Payment Fare Formula

```
TotalFee = BaseFee(VehicleType)
         + DistanceKm × RatePerKm(VehicleType)  
         + WeightSurcharge

WeightSurcharge = max(0, ActualWeightKg - ThresholdKg(VehicleType)) × SurchargeRate
```
Values (BaseFee, RatePerKm, ThresholdKg, SurchargeRate) → from config/DB, không hardcode.

## Shipment Saga Retry Policy

```
Step fails → retry mỗi 30s, tối đa 5 lần
After 5 failures:
  → publish compensating events (reverse order)
  → Order.UpdateStatus(Pending)
  → Driver.UpdateStatus(Idle) [if assigned]
```
