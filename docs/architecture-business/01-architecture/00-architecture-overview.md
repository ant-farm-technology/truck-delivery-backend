# 🏗️ 1. Tổng quan kiến trúc hệ thống

---

## 🧠 Kiến trúc tổng thể
```
Client (Web / Mobile)
        ↓
API Gateway (YARP)
        ↓
-----------------------------
|     Backend Services      |
-----------------------------
        ↓
Event Bus (Kafka)
        ↓
-----------------------------
| Async Processing Services |
-----------------------------
        ↓
Databases / Cache / External
```

## 🎯 Kiến trúc chính sử dụng

- Microservices + Domain isolation
- Event-driven architecture (Kafka làm backbone)
- CQRS (read/write separation)
- Polyglot services (.NET + Rust + Python)
- Eventually consistent system

---

---

# 🧩 2. Phân lớp hệ thống

---

## 2.1 Edge Layer

|Component|Vai trò|
|---|---|
|API Gateway|Entry point, routing, auth|
|Client apps|Web + Mobile|

---

---

## 2.2 Service Layer

- Core business services (.NET)
- Specialized compute services (Rust / Python)

---

---

## 2.3 Platform Layer

- Event Bus
- Outbox / Consumer runtime
- Observability
- Infra services

---

---

## 2.4 Data Layer

- MySQL (OLTP)
- MongoDB (read model, tracking)
- PostGIS (spatial)
- Redis (cache, idempotency)

---

---

# 🌐 3. API Gateway

---

## Công nghệ

- YARP

---

## Vai trò

- Reverse proxy
- Routing request đến service
- JWT validation
- Inject CorrelationId
- Rate limiting

---

---

## Không làm

❌ Không chứa business logic  
❌ Không gọi nhiều service sync chain dài

---

---

# 🧠 4. Core Microservices (.NET)

---

## 4.1 Identity Service

---

### Vai trò

- Authentication / Authorization
- JWT issuing
- User management

---

### Công nghệ

- ASP.NET Core
- Identity / OAuth2
- MySQL

---

---

## 4.2 Order Service

---

### Vai trò

- Entry point cho order
- Persist order state
- Emit domain events

---

### Công nghệ

- ASP.NET Core
- EF Core (write)
- Dapper (read)
- Outbox pattern

---

---

## 4.3 Driver Service

---

### Vai trò

- Quản lý driver profile
- Driver state

---

---

## 4.4 Fleet Service

---

### Vai trò

- Vehicle management
- Driver ↔ Vehicle mapping
- Availability state

---

---

## 4.5 Shipment Service

---

### Vai trò

- Điều phối workflow
- Orchestrate Saga

---

---

## 4.6 Tracking Service

---

### Vai trò

- Realtime location streaming
- Session tracking

---

### Công nghệ

- ASP.NET Core
- SignalR (WebSocket)
- MongoDB

---

---

## 4.7 Notification Service

---

### Vai trò

- Gửi thông báo (push, email, SMS)

---

---

## 4.8 Payment Service

---

### Vai trò

- Payment processing
- Integration external gateway

---

---

# ⚙️ 5. Specialized Services

---

## 5.1 Routing Service (Rust)

---

### Vai trò

- Tính toán tuyến đường
- Xử lý spatial queries

---

### Công nghệ

- Rust (hiệu năng cao)
- PostGIS
- OpenStreetMap

---

---

## 5.2 Optimization Service (Python)

---

### Vai trò

- Tối ưu assignment
- Giải bài toán routing

---

### Công nghệ

- Python
- Google OR-Tools

---

---

# 📡 6. Event Bus Layer

---

## Công nghệ

- Apache Kafka

---

## Vai trò

- Backbone communication
- Decouple services
- Async processing

---

---

## Patterns sử dụng

- Publish / Subscribe
- Event sourcing (partial)
- Replay

---

---

# 🔁 7. Platform Layer

---

## 7.1 Event Bus Abstraction

---

- Wrapper Kafka
- Standardize publish/consume

---

---

## 7.2 Outbox Worker

---

- Publish event từ DB → Kafka
- Retry + batching

---

---

## 7.3 Consumer Runtime

---

- Idempotency
- Retry
- DLQ

---

---

## 7.4 Observability

---

### Stack

- Prometheus
- Grafana
- Grafana Loki
- Grafana Tempo

---

---

# 🗄️ 8. Data Layer

---

## 8.1 MySQL

---

### Vai trò

- OLTP
- Write model (CQRS)

---

---

## 8.2 MongoDB

---

### Vai trò

- Read model
- Tracking data

---

---

## 8.3 PostGIS

---

### Vai trò

- Spatial queries
- Routing computation

---

---

## 8.4 Redis

---

### Vai trò

- Cache
- Idempotency store
- Rate limit

---

---

# 🔄 9. Communication Patterns

---

## 9.1 Sync (HTTP)

---

Client → Gateway → Service

---

---

## 9.2 Async (Kafka)

---

Service → Kafka → Service

---

---

## 9.3 Không dùng

❌ Service A → Service B → Service C (sync chain)

---

---

# 🧠 10. Data Flow Strategy

---

## Write

Command → Service → DB → Outbox → Kafka

---

---

## Read

Query → Read DB (MySQL replica / MongoDB)

---

---

# ⚡ 11. Scalability Strategy

---

## Horizontal scaling

- Stateless services
- Scale theo load

---

---

## Kafka scaling

- Partition-based

---

---

## DB scaling

- Read replica
- Sharding (future)

---

---

# 🔐 12. Security

---

- JWT authentication
- Gateway validation
- Service-to-service auth (internal)

---

---

# 🧪 13. Testing Strategy (High-level)

---

- Unit tests
- Integration tests
- Contract tests
- Event-driven tests

---

---

# ⚠️ 14. Trade-offs

---

## Ưu điểm

✔ Highly scalable  
✔ Loose coupling  
✔ Fault-tolerant

---

---

## Nhược điểm

❌ Complexity cao  
❌ Debug khó  
❌ Eventual consistency

---

---

# 🎯 15. Design Guarantees

---

Hệ thống này đảm bảo:

- Không single point of failure
- Dễ scale theo từng domain
- Resilient trước failure
- Linh hoạt công nghệ

---

---

# ✅ Kết luận

Đây là kiến trúc:

> **Microservices + Event-driven + Polyglot + Production-ready**

---

## Insight quan trọng nhất

> **Kafka không chỉ là message queue — nó là “xương sống” của toàn bộ hệ thống**