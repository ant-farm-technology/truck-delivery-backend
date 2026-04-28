# Tracking Service Agent — Realtime GPS Expert

Bạn là chuyên gia về **Tracking Service** trong hệ thống Truck Delivery. Service này xử lý real-time GPS từ tài xế và broadcast tới khách hàng.

## Context

Tracking Service là high-throughput, eventually consistent:
- **Input:** Driver app POST `/api/v1/tracking/location` mỗi 5–10 giây
- **Scale:** 10k drivers = ~10k–50k events/sec
- **Storage:** MongoDB với TTL index (24h hot data)
- **Realtime:** SignalR WebSocket broadcast tới Customer

## Architecture Pipeline

```
Driver App (REST, 5–10s interval)
    ↓ POST /api/v1/tracking/location
    ↓ TrackingController → IMediator.Send(UpdateLocationCommand)
    ↓ Validate (lat/lng range, driverId exists)
    ↓ [Optional] Map matching → snap to road
    ↓ Save to MongoDB (TrackingPoints collection)
    ↓ Publish LocationUpdated event (Kafka: tracking.location.updated)
    ↓ Broadcast via SignalR Hub → Customer WebSocket
```

## Domain Model

```csharp
// MongoDB documents
public sealed class TrackingPoint
{
    [BsonId] public ObjectId Id { get; set; }
    public Guid DriverId { get; set; }
    public Guid? ShipmentId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Speed { get; set; }      // km/h
    public double? Heading { get; set; }   // degrees 0-360
    public DateTime Timestamp { get; set; }
    public DateTime CreatedAt { get; set; } // TTL field
}

public sealed class TrackingSession
{
    [BsonId] public Guid SessionId { get; set; }
    public Guid DriverId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ShipmentId { get; set; }
    public TrackingSessionStatus Status { get; set; } // Active, Completed
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
```

## MongoDB Index Design

```javascript
// TrackingPoints collection
db.TrackingPoints.createIndex({ "DriverId": 1, "Timestamp": -1 })
db.TrackingPoints.createIndex({ "ShipmentId": 1, "Timestamp": -1 })
db.TrackingPoints.createIndex(
    { "CreatedAt": 1 }, 
    { expireAfterSeconds: 86400 }  // TTL: 24h
)
db.TrackingPoints.createIndex({ "location": "2dsphere" })  // geo index
```

## SignalR Hub

```csharp
public sealed class TrackingHub : Hub
{
    // Client subscribe to a shipment's tracking
    public async Task SubscribeToShipment(Guid shipmentId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"tracking:{shipmentId}");

    public async Task SubscribeToDriver(Guid driverId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"tracking:{driverId}");

    public async Task Unsubscribe(Guid shipmentId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tracking:{shipmentId}");
}

// Broadcast from service
public sealed class TrackingBroadcaster : ITrackingBroadcaster
{
    private readonly IHubContext<TrackingHub> _hub;

    public async Task BroadcastLocationAsync(Guid shipmentId, LocationUpdate update, CancellationToken ct)
    {
        // Push delta only, not full state
        await _hub.Clients.Group($"tracking:{shipmentId}")
            .SendAsync("LocationUpdated", new
            {
                lat = update.Latitude,
                lng = update.Longitude,
                speed = update.Speed,
                eta = update.EstimatedTimeArrival,
                timestamp = update.Timestamp
            }, ct);
    }
}
```

## Redis Backplane (SignalR Scale-out)

```csharp
// Program.cs
builder.Services.AddSignalR()
    .AddStackExchangeRedis(connectionString, options =>
    {
        options.Configuration.ChannelPrefix = "TruckDelivery";
    });
```

## Kafka Events

**Consumes:**
- `shipment.driver.assigned` → `StartTrackingSessionCommand`
- `order.order.status-updated` (Delivered) → `EndTrackingSessionCommand`

**Publishes:**
- `tracking.location.updated` → `LocationUpdatedEvent { ShipmentId, DriverId, Lat, Lng, Timestamp }`

## Rate Limiting & Backpressure

```csharp
// Per driver: max 1 request per 2s (min interval)
// Drop points that arrive too fast (duplicate protection)
// Use Redis for rate limiting key: tracking:rate:{driverId}

public sealed class TrackingRateLimiter : ITrackingRateLimiter
{
    public async Task<bool> AllowAsync(Guid driverId, CancellationToken ct)
    {
        var key = $"tracking:rate:{driverId}";
        var set = await _redis.StringSetAsync(key, 1, TimeSpan.FromSeconds(2), When.NotExists);
        return set; // false = rate limited
    }
}
```

## Performance Rules

- **Stateless service** → scale horizontal pods
- **Không lưu GPS vĩnh viễn** → TTL 24h, archive > 24h to cold storage
- **Không broadcast full history** → push delta only
- **Drop abnormal GPS jumps** → validate speed (max 150 km/h)
- **Map matching optional** → expensive, only for complex road networks
- Tracking Service **KHÔNG thay đổi order state**
- Tracking Service **KHÔNG tính route**

## ETA Calculation (Optional)

```csharp
// Derived from Routing Context data + current speed
public double CalculateEta(RouteProgress progress, double currentSpeed)
{
    if (currentSpeed <= 0) return -1; // unknown
    return progress.RemainingDistanceKm / currentSpeed * 60; // minutes
}
```
