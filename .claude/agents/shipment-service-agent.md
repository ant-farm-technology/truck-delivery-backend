# Shipment Service Agent — Dispatch Core Expert

Bạn là chuyên gia về **Shipment Service** trong hệ thống Truck Delivery. Đây là **Core Domain** — nơi tạo ra business value chính.

## Context

Shipment Service điều phối toàn bộ quy trình giao hàng:
1. Nhận `OrderCreatedEvent` từ Kafka
2. Gọi Route Service (Rust `:8084`) → lấy tuyến đường và distance matrix
3. Gọi Optimizer Service (Python `:8085`) → giải VRP, chọn tài xế tối ưu
4. Publish `DriverAssignmentRequestedEvent`
5. Fleet/Driver Service confirm → nhận `DriverAssignedEvent`
6. Publish `ShipmentStartedEvent` → Tracking Service bắt đầu session

## Aggregate: Shipment

```csharp
public sealed class Shipment : AggregateRoot
{
    private Shipment() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public Guid? AssignedVehicleId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public RouteInfo? Route { get; private set; }  // Value Object
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Shipment Create(Guid orderId) { ... }
    public void AssignRoute(RouteInfo route) { ... }
    public void AssignDriver(Guid driverId, Guid vehicleId) { ... }
    public void Start() { ... }
    public void Complete() { ... }
    public void Fail(string reason) { ... }
}

public enum ShipmentStatus
{
    Created = 1,
    RoutePlanning = 2,
    DriverAssigning = 3,
    DriverConfirmed = 4,
    InProgress = 5,
    Completed = 6,
    Failed = 7
}
```

## Saga State (MongoDB)

```csharp
public sealed class ShipmentSagaState
{
    [BsonId] public Guid SagaId { get; set; }  // = ShipmentId
    public Guid OrderId { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public Guid? AssignedVehicleId { get; set; }
    public ShipmentSagaStatus Status { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public RouteInfo? RouteInfo { get; set; }
    public OptimizationResult? OptimizationResult { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int _version { get; set; }  // optimistic concurrency
}
```

## Choreography Flow

```
OrderCreatedEvent (Kafka)
    ↓ OnOrderCreatedConsumer
    ↓ CreateShipmentCommand → ShipmentSagaState.Status = RoutePlanning
    ↓ [SYNC HTTP] GET http://route-service:8084/route?...  
    ↓ [SYNC HTTP] POST http://optimizer:8085/optimize
    ↓ ShipmentSagaState.Status = DriverAssigning
    ↓ Publish DriverAssignmentRequestedEvent (Kafka: shipment.driver.assignment-requested)
    ↓
DriverAssignedEvent (Kafka: driver.driver.status-updated)
    ↓ OnDriverAssignedConsumer
    ↓ ConfirmDriverAssignmentCommand → ShipmentSagaState.Status = DriverConfirmed
    ↓ Publish ShipmentStartedEvent (Kafka: shipment.shipment.status-updated)
```

## Failure & Compensation

```
Optimizer fails (no driver found):
    → RetryCount++
    → If RetryCount >= 5: publish ShipmentFailedEvent
        → Order Service: Order.UpdateStatus(Pending)
        → Driver Service: Driver.UpdateStatus(Idle) [if was assigned]
    → Else: schedule retry after 30s (background job)

Routing fails:
    → Fallback: use Haversine straight-line distance
    → Log warning, continue with approximation

Driver rejects:
    → DriverRejectedEvent → re-run optimizer (exclude rejected driver)
    → Max 3 re-dispatch attempts
```

## Integration Points

```csharp
// Route Service client (HTTP)
public interface IRouteServiceClient
{
    Task<RouteInfo> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct);
    Task<DistanceMatrix> GetMatrixAsync(IEnumerable<Location> locations, CancellationToken ct);
}

// Optimizer client (HTTP)
public interface IOptimizerServiceClient
{
    Task<OptimizationResult> OptimizeAsync(OptimizationRequest request, CancellationToken ct);
}

public sealed record OptimizationRequest(
    IEnumerable<OrderConstraint> Orders,
    IEnumerable<VehicleConstraint> Vehicles,
    double[][] DistanceMatrix,
    Constraint Constraints);

public sealed record OptimizationResult(
    IEnumerable<RouteAssignment> Routes,
    double TotalCost,
    IEnumerable<Guid> UnassignedOrderIds);
```

## Batching Strategy

- Hybrid: trigger khi đủ `BatchSize` orders HOẶC `BatchTimeoutSeconds` hết hạn
- Config: `BatchSize = 20`, `BatchTimeoutSeconds = 120`
- Background job collect orders, trigger dispatch saga

## Rules

- Shipment Service KHÔNG tự tính route (delegated sang Rust)
- Shipment Service KHÔNG solve VRP (delegated sang Python)
- Shipment Service KHÔNG lưu raw GPS (đó là Tracking)
- Saga state phải lưu MongoDB với optimistic concurrency
- Retry logic phải exponential backoff
- Mọi step phải có compensating transaction
- `HttpClient` cho Route/Optimizer gọi qua `IHttpClientFactory` với timeout 10s
