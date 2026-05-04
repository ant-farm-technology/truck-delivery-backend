## 1. Mục tiêu

Tracking Context chịu trách nhiệm:

- Nhận dữ liệu GPS từ driver
- Xử lý và chuẩn hoá vị trí
- Broadcast realtime cho client
- Lưu trữ lịch sử (có kiểm soát)

---

## 2. Boundary & Responsibility

---

### Thuộc Tracking

- Ingest GPS (high frequency)  
- Map matching (optional nhẹ)  
- Realtime streaming (SignalR)  
- Tracking history (MongoDB)

---

### KHÔNG thuộc Tracking

- Không quyết định route  
- Không assign driver  
- Không tối ưu tuyến  
- Không thay đổi trạng thái order

Tracking chỉ **phản ánh reality**, không điều khiển system

---

## 3. Ubiquitous Language

Location  
TrackingPoint  
TrackingSession  
RouteProgress  
Heartbeat

---

## 4. Domain Model

---

### 4.1 TrackingPoint

TrackingPoint  
 ├── DriverId  
 ├── Latitude  
 ├── Longitude  
 ├── Speed  
 ├── Heading  
 ├── Timestamp

---

### 4.2 TrackingSession

TrackingSession  
 ├── SessionId  
 ├── DriverId  
 ├── OrderId  
 ├── Status (Active, Completed)  
 ├── StartedAt  
 └── EndedAt

---

### 4.3 RouteProgress

RouteProgress  
 ├── CurrentNode  
 ├── RemainingDistance  
 ├── ETA

---

## 5. Data Ingestion (CRITICAL)

---

### Endpoint

POST /tracking/location

---

### Input

{  
  "driverId": "uuid",  
  "lat": 0,  
  "lng": 0,  
  "speed": 40,  
  "timestamp": "..."  
}

---

### Rate

1–5s / request / driver

---

Với 10k drivers:

~10k – 50k events/sec

---

## 6. Processing Pipeline

---

Driver App  
   ↓  
Tracking API  
   ↓  
Validation  
   ↓  
(Optional) Map Matching  
   ↓  
Publish event (Kafka)  
   ↓  
Realtime Broadcast (SignalR)  
   ↓  
Store (MongoDB)

---

---

## 7. Realtime Streaming (SignalR)

---

### Tech

- ASP.NET SignalR

---

### Channel Design

tracking:{orderId}  
tracking:{driverId}

---

### Payload

{  
  "lat": 0,  
  "lng": 0,  
  "eta": 120  
}

---

### Rule

Chỉ push delta (thay đổi), không spam full state

---

---

## 8. Map Matching (Optional)

---

### Khi cần:

- GPS noise cao  
- Đường phức tạp

---

### Strategy

Snap to nearest road segment

---

Có thể reuse từ Routing Context

---

---

## 9. Storage Strategy (MongoDB)

---

### Collection: TrackingPoints

{  
  "driverId": "...",  
  "location": { "type": "Point", "coordinates": [lng, lat] },  
  "timestamp": "...",  
  "ttl": 86400  
}

---

### Index

- Geo index  
- TTL index

---

---

## 10. Data Retention Policy

---

### Hot data

0–24h → MongoDB

---

### Cold data

> 24h → archive (S3 / data lake)

---

### Rule

Không lưu vô hạn

---

---

## 11. Performance & Scaling

---

### 11.1 Horizontal scaling

Stateless service → scale pod

---

### 11.2 Backpressure

- Rate limit per driver  
- Drop duplicate points

---

### 11.3 Compression

- Reduce payload size

---

---

## 12. Integration Points

---

### Publish

LocationUpdated

---

### Consume

DriverAssigned → start session  
OrderCompleted → end session

---

---

## 13. Derived Data (Optional)

---

### ETA Calculation

Distance (Routing) + Speed

---

### Progress %

distance_travelled / total_distance

---

---

## 14. Consistency Model

---

Eventually consistent

---

Không cần:

Strong consistency  
Transaction

---

---

## 15. Failure Handling

---

### Case 1: Missing GPS

→ mark stale

---

### Case 2: Out-of-order events

→ sort by timestamp

---

### Case 3: Spike traffic

→ drop low-priority updates

---

---

## 16. Anti-patterns

---

Lưu toàn bộ GPS vĩnh viễn  
Broadcast toàn bộ history  
Không rate limit  
Tính toán nặng trong pipeline  
Coupling với Dispatch

---

---

## 17. Design Guarantees

---

Tracking Context đảm bảo:

- High throughput ingestion
- Realtime delivery
- Controlled storage growth
- Không ảnh hưởng core system