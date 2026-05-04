## 1. 🎯 Mục tiêu

- Triển khai hệ thống microservices ở production
- Đảm bảo **high availability + scalability + resilience**
- Chuẩn hoá cách deploy & vận hành

---

## 2. 🧠 Tổng quan kiến trúc triển khai

```
Internet / Mobile / Web
        ↓
   Load Balancer (L7)
        ↓
   Ingress Controller
        ↓
-------------------------------
|       Kubernetes Cluster    |
|                             |
|  API Gateway (YARP)         |
|  Microservices (.NET)       |
|  Routing (Rust)             |
|  Optimization (Python)      |
|                             |
|  Kafka Cluster              |
|  Redis Cluster              |
|                             |
-------------------------------
        ↓
 Managed Databases (MySQL, MongoDB, PostGIS)
```

## 3. 🌐 Edge Layer

---

### 3.1 Load Balancer

---

#### Vai trò

- Entry point từ Internet
- TLS termination (optional)
- Routing đến cluster

---

---

### 3.2 Ingress Controller

---

#### Công nghệ phổ biến

- NGINX Ingress
- Traefik

---

---

#### Vai trò

- HTTP routing
- Domain-based routing
- SSL termination

---

---

## 4. ☸️ Kubernetes Cluster

---

### 4.1 Node Structure

---

Master Nodes (control plane)  
Worker Nodes (run pods)

---

---

### 4.2 Namespace Strategy

---

production  
staging  
development  
monitoring

---

---

## 5. 🚪 API Gateway Deployment

---

### Component

- YARP

---

---

### Deployment

replicas: 3  
autoscaling: enabled

---

---

### Role

- Entry point nội bộ
- Routing đến services
- Auth validation

---

---

## 6. 🧩 Microservices Deployment

---

### Pattern

1 service = 1 deployment

---

---

### Example

replicas: 2–5  
resources:  
  cpu: 200m–1CPU  
  memory: 256MB–1GB

---

---

### Scaling

- Horizontal Pod Autoscaler (HPA)

---

---

## 7. ⚙️ Specialized Services

---

### Routing Service (Rust)

---

- CPU intensive
- Deploy riêng node pool (optional)

---

---

### Optimization Service (Python)

---

- Compute-heavy
- Có thể scale riêng

---

---

## 8. 📡 Kafka Deployment

---

### Công nghệ

- Apache Kafka

---

---

### Mode

Cluster (3–5 brokers)

---

---

### Config

- Replication factor: 3
- Partition: tùy load

---

---

### Storage

- Persistent Volume (SSD)

---

---

## 9. 🗄️ Database Layer

---

### 9.1 MySQL

---

- Managed (AWS RDS / Cloud SQL)
- Primary + Read replica

---

---

### 9.2 MongoDB

---

- Replica set

---

---

### 9.3 PostGIS

---

- Dedicated instance

---

---

## 10. ⚡ Redis Deployment

---

### Mode

- Cluster hoặc Sentinel

---

---

### Use cases

- Cache
- Idempotency
- Rate limit

---

---

## 11. 📊 Observability Stack

---

### Components

- Prometheus
- Grafana
- Grafana Loki
- Grafana Tempo

---

---

### Deployment

Namespace: monitoring

---

---

## 12. 🔄 CI/CD Pipeline

---

### Flow

Code → Build → Test → Docker Image → Push Registry → Deploy (K8s)

---

---

### Tools

- GitHub Actions / GitLab CI
- Docker Registry

---

---

## 13. 🔐 Security

---

### External

- HTTPS
- WAF (optional)

---

---

### Internal

- NetworkPolicy
- Service-to-service auth

---

---

## 14. ⚡ Autoscaling Strategy

---

### HPA

---

metrics:  
  cpu: 70%

---

---

### Kafka scaling

- Add partition
- Add consumer

---

---

### Node autoscaling

- Cluster autoscaler

---

---

## 15. 🧠 Networking

---

### Service communication

ClusterIP (internal)

---

---

### External access

Ingress → Gateway

---

---

## 16. 💾 Storage

---

### Persistent

- Kafka
- DB

---

---

### Stateless

- Microservices

---

---

## 17. 🔄 Deployment Strategy

---

### Rolling update

Zero downtime

---

---

### Canary (optional)

Release từng phần

---

---

## 18. ⚠️ Failure Handling

---

- Pod restart (liveness probe)
- Circuit breaker
- Retry

---

---

## 19. 📈 Capacity Planning

---

### Monitor

- CPU
- Memory
- Kafka lag
- DB load

---

---

### Scale trước khi quá tải

Scale proactive

---

---

## 20. ⚠️ Anti-patterns

---

❌ Deploy tất cả trên 1 node  
❌ Không có autoscaling  
❌ Không persistent storage  
❌ Không monitoring  
❌ Hardcode config

---

---

## 21. 🎯 Design Guarantees

---

Kiến trúc này đảm bảo:

- High availability
- Fault tolerance
- Horizontal scalability
- Observability đầy đủ

---

---

# ✅ Kết luận

Deployment architecture này là:

> **Production-grade Kubernetes architecture cho hệ thống event-driven**

---

## Insight quan trọng nhất

> **Bạn không deploy service — bạn deploy một hệ sinh thái phân tán**