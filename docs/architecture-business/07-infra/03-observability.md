## 1. Mục tiêu

Observability giúp bạn:

- Hiểu trạng thái hệ thống realtime
- Debug nhanh khi có lỗi
- Phân tích performance & bottleneck
- Theo dõi business metrics

---

## 2. 3 Pillars of Observability

---

### 2.1 Metrics

Số liệu định lượng  
→ QPS, latency, error rate

---

### 2.2 Logs

Chi tiết từng event / request

---

### 2.3 Traces

Theo dõi request xuyên suốt system

---

---

## 3. Observability Stack

---

### Metrics

- Prometheus

---

### Logging

- Grafana Loki

---

### Tracing

- OpenTelemetry
- Grafana Tempo

---

### Visualization

- Grafana

---

---

## 4. Correlation Strategy (CỰC KỲ QUAN TRỌNG)

---

### CorrelationId

X-Correlation-Id: uuid

---

### Flow

API Gateway  
  ↓  
All services  
  ↓  
Kafka events  
  ↓  
Logs + traces

---

Rule:

Mỗi request = 1 correlationId xuyên suốt

---

---

## 5. Metrics Design

---

### 5.1 System Metrics

- CPU  
- Memory  
- Disk

---

---

### 5.2 Application Metrics

---

#### Order

order_created_total  
order_assigned_total  
order_failed_total

---

---

#### Dispatch

dispatch_latency_ms  
driver_assignment_success_rate

---

---

#### Tracking

location_events_per_sec

---

---

#### Payment

payment_success_rate  
payment_latency

---

---

### 5.3 Kafka Metrics

consumer_lag  
messages_per_sec

---

---

## 6. Golden Signals

---

4 metrics quan trọng nhất:

1. Latency  
2. Traffic  
3. Errors  
4. Saturation

---

---

## 7. Logging Strategy

---

### Structured Logging

{  
  "timestamp": "...",  
  "level": "INFO",  
  "service": "order-service",  
  "correlationId": "...",  
  "message": "Order created",  
  "orderId": "..."  
}

---

---

### Logging Rules

- Không log plain text  
- Log dạng JSON  
- Có correlationId

---

---

### Log Levels

|Level|Use|
|---|---|
|INFO|flow bình thường|
|WARN|abnormal|
|ERROR|lỗi|
|DEBUG|dev only|

---

---

## 8. Distributed Tracing

---

### Tech

- OpenTelemetry

---

---

### Flow Example

CreateOrder  
  ↓  
Dispatch  
  ↓  
Routing  
  ↓  
Optimization  
  ↓  
Fleet

---

---

### Span Structure

Trace  
 ├── Span (API)  
 ├── Span (Order Service)  
 ├── Span (Dispatch)  
 ├── Span (Routing)

---

---

### Rule

Mỗi service = 1 span

---

---

## 9. Event Observability

---

### Problem

Event async → khó debug

---

---

### Solution

- Gắn correlationId vào event  
- Log eventId

---

---

### Kafka Trace

OrderCreated  
 → DriverAssigned  
 → OrderAssigned

---

---

## 10. Alerting Strategy

---

### Tool

- Prometheus + Alertmanager

---

---

### Critical Alerts

---

#### High error rate

error_rate > 5%

---

---

#### High latency

p95 > 2s

---

---

#### Kafka lag

lag > threshold

---

---

#### Service down

health check fail

---

---

## 11. Dashboards

---

### Grafana Dashboards

---

#### System Dashboard

- CPU  
- Memory

---

---

#### Business Dashboard

- Orders per minute  
- Delivery time

---

---

#### Kafka Dashboard

- Throughput  
- Lag

---

---

## 12. Sampling Strategy

---

### Problem

Tracing full = tốn tài nguyên

---

---

### Solution

- Sample 10–20%  
- Full trace khi error

---

---

## 13. Log & Trace Volume Control

---

### Strategy

- Drop debug logs ở production  
- Retention policy (7–30 ngày)

---

---

## 14. Security & Privacy

---

### Rule

Không log:  
- password  
- token  
- PII

---

---

## 15. Debugging Workflow

---

### Khi có lỗi

1. Tìm correlationId  
2. Xem trace  
3. Check logs từng service  
4. Check metrics

---

---

## 16. Anti-patterns

---

Không có correlationId  
Log không structured  
Không trace cross-service  
Alert quá nhiều (alert fatigue)  
Không có dashboard

---

---

## 17. Design Guarantees

---

Observability đảm bảo:

- Debug nhanh
- Biết bottleneck ở đâu
- Phát hiện lỗi sớm
- Hiểu behavior hệ thống