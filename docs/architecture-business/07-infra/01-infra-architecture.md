## 1. Mục tiêu

Infra Architecture định nghĩa:

- Cách deploy hệ thống
- Cách scale từng service
- Messaging, cache, DB
- Observability & reliability

---

## 2. High-level Deployment

---

                 ┌────────────────────┐
                 │     Internet       │
                 └────────┬───────────┘
                          │
                    Load Balancer
                          │
                 ┌────────▼────────┐
                 │   API Gateway   │ (YARP)
                 └────────┬────────┘
                          │
     ┌────────────────────┼────────────────────┐
     │                    │                    │
 Order Service     Driver Service     Tracking Service
     │                    │                    │
     └────────────┬───────┴────────────┬───────┘
                  │                    │
               Kafka Cluster      Redis Cluster
                  │                    │
        ┌─────────┼──────────┐         │
        │         │          │         │
   Payment   Notification   Saga     Cache

---

---

## 3. Compute Layer

---

### Containerization

- Docker cho tất cả services

---

### Orchestration

- Kubernetes (K8s)

---

### Deployment Strategy

- Rolling update  
- Blue/Green (critical services)

---

---

## 4. API Gateway

---

### Tech

- YARP

---

### Features

- Routing  
- JWT validation  
- Rate limiting  
- CorrelationId injection

---

---

## 5. Messaging Layer

---

### Core

- Apache Kafka

---

### Cluster Setup

- 3–5 brokers  
- Replication factor = 3  
- Partition theo key (orderId)

---

---

### Topic Design

order.events  
fleet.events  
payment.events  
tracking.events

---

---

### Producer Strategy

- Outbox pattern (DB → Kafka)  
- Async publish

---

---

### Consumer Strategy

- Consumer group  
- Idempotent processing

---

---

## 6. Data Layer

---

### 6.1 Write DB

- MySQL (Primary + Replica)

---

### 6.2 Read DB

- MySQL replica (Dapper)
- MongoDB (projection + saga state)

---

### 6.3 Spatial

- PostGIS (Routing service)

---

---

### DB Strategy

- Mỗi service 1 database  
- Không shared DB

---

---

## 7. Cache Layer

---

### Tech

- Redis Cluster

---

### Use cases

- Idempotency  
- Rate limiting  
- Caching hot data

---

---

### Strategy

- TTL-based  
- Cache-aside

---

---

## 8. Realtime Layer

---

### Tech

- SignalR

---

### Scaling

- Backplane Redis

---

---

## 9. Observability Stack

---

### Metrics

- Prometheus

---

### Logging

- Grafana Loki

---

### Tracing

- OpenTelemetry → Grafana Tempo

---

---

### Correlation

X-Correlation-Id xuyên suốt request/event

---

---

## 10. Resilience Patterns

---

### Circuit Breaker

- Ngắt khi downstream fail

---

---

### Retry

- Exponential backoff

---

---

### Timeout

- 2–5s cho sync call

---

---

### Bulkhead

- Tách resource theo service

---

---

## 11. Security

---

### Auth

- JWT (Identity service)

---

### Network

- Internal services không expose public  
- Dùng private network

---

---

### Secrets

- Kubernetes Secrets / Vault

---

---

## 12. CI/CD

---

### Pipeline

Code → Build → Test → Docker → Deploy

---

---

### Strategy

- Canary release  
- Rollback nhanh

---

---

## 13. Scaling Strategy

---

### Horizontal Scaling

- Stateless services scale dễ

---

---

### Stateful

- DB scale bằng replica

---

---

### Kafka

- Scale bằng partition

---

---

## 14. Data Consistency

---

Eventual consistency

---

### Strategy

- Saga  
- Outbox

---

---

## 15. Backup & Recovery

---

### DB

- Daily backup  
- PITR (point-in-time recovery)

---

---

### Kafka

- Retention policy

---

---

## 16. Cost Optimization

---

- Auto-scale down  
- Spot instances (non-critical)

---

---

## 17. Anti-patterns

---

Shared database  
Sync chain giữa service  
Không monitoring  
Không backup  
Single Kafka broker  
Không partition key

---

---

## 18. Design Guarantees

---

Infra Architecture đảm bảo:

- High availability
- Scalable
- Observable
- Resilient