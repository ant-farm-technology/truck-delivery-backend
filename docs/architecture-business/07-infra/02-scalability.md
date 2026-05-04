## 1. Mục tiêu

Scalability đảm bảo:

- Hệ thống xử lý được traffic tăng trưởng
- Không degrade performance nghiêm trọng
- Scale theo từng thành phần độc lập

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Scale theo bottleneck, không scale toàn hệ

Tracking high load → scale Tracking  
NOT scale Payment

---

---

### 2.2 Stateless trước, stateful sau

Stateless service → scale dễ  
Stateful → scale phức tạp

---

---

### 2.3 Horizontal > Vertical

Scale out (nhiều instance)  
> scale up (CPU/RAM)

---

---

## 3. Load Profile của hệ thống

---

### Write-heavy

- Tracking (location updates)  
- Orders (create/update)

---

---

### Read-heavy

- Order query  
- Tracking UI

---

---

### Burst traffic

- Peak hours  
- Promotion

---

---

## 4. Service-level Scaling

---

### 4.1 Order Service

---

#### Characteristics

- Moderate load  
- Write + read

---

#### Strategy

- Scale read (replica)  
- Cache hot data

---

---

### 4.2 Tracking Service (HOT PATH)

---

#### Characteristics

- 10k–50k events/sec

---

#### Strategy

- Horizontal scale pods  
- Kafka buffer  
- Batch processing

---

---

### 4.3 Dispatch / Optimization

---

#### Characteristics

- CPU-heavy (solver)

---

#### Strategy

- Worker pool  
- Queue-based  
- Limit concurrency

---

---

### 4.4 Payment

---

#### Characteristics

- IO-bound  
- External dependency

---

#### Strategy

- Rate limit  
- Retry + circuit breaker

---

---

## 5. Kafka Scaling

---

### Core

- Apache Kafka

---

### Partition Strategy

partition key = orderId / driverId

---

---

### Scaling Rule

Throughput ∝ number of partitions

---

---

### Consumer Scaling

consumer instances ≤ partitions

---

---

## 6. Database Scaling

---

### 6.1 MySQL

---

#### Strategy

- Primary (write)  
- Replica (read)

---

---

### 6.2 Sharding (khi lớn)

---

Shard theo:  
- orderId  
- region

---

---

### 6.3 MongoDB

---

- Shard theo key  
- Dùng cho projection

---

---

## 7. Cache Scaling

---

### Redis Cluster

---

- Partition data  
- Scale node

---

---

### Use cases

- Idempotency  
- Hot queries  
- Session

---

---

## 8. Realtime Scaling

---

### SignalR

---

#### Problem

Nhiều connection (10k+)

---

---

#### Solution

- Scale horizontally  
- Redis backplane

---

---

## 9. Auto Scaling (K8s)

---

### HPA (Horizontal Pod Autoscaler)

---

Scale theo:  
- CPU  
- Memory  
- Custom metrics (QPS)

---

---

### Example

Tracking pods:  
min=3  
max=50

---

---

## 10. Backpressure & Queueing

---

### Problem

Producer nhanh hơn consumer

---

---

### Solution

- Kafka buffer  
- Reject / slow down producer

---

---

## 11. Hotspot Handling

---

### Case

1 order quá nhiều event

---

---

### Solution

- Partition theo key khác  
- Throttle

---

---

## 12. Cost vs Performance

---

### Trade-off

Low latency ↔ High cost

---

---

### Strategy

- Cache nhiều hơn  
- Async processing

---

---

## 13. Multi-region Scaling (future)

---

### Strategy

- Region-based deployment  
- Data replication

---

---

## 14. Anti-patterns

---

Scale tất cả service cùng lúc  
Không dùng cache  
Single DB instance  
Không partition Kafka  
Không load test

---

---

## 15. Load Testing

---

### Tools

- k6  
- JMeter

---

---

### Scenarios

- Peak traffic  
- Spike test  
- Soak test

---

---

## 16. Metrics quan trọng

---

- QPS  
- Latency (p95, p99)  
- Error rate  
- Kafka lag

---

---

## 17. Design Guarantees

---

Scalability đảm bảo:

- System scale tuyến tính
- Không bottleneck đơn lẻ
- Giữ được latency ổn định