## 1. 🎯 Mục tiêu

Tài liệu này định nghĩa:

- SLA (Service Level Agreement) — cam kết với user/business
- SLO (Service Level Objective) — mục tiêu nội bộ
- SLI (Service Level Indicator) — cách đo lường

---

## 2. 🧠 Khái niệm cốt lõi

---

### SLA

Cam kết với khách hàng (legal / business)

---

---

### SLO

Target nội bộ cần đạt

---

---

### SLI

Metric đo lường thực tế

---

---

## 3. 📊 Service Level Indicators (SLI)

---

### 3.1 Availability

% request thành công

---

---

### 3.2 Latency

Thời gian response

---

---

### 3.3 Throughput

Số request / giây

---

---

### 3.4 Error Rate

% request lỗi

---

---

### 3.5 Event Processing Lag

Kafka lag (consumer delay)

---

---

## 4. 🎯 SLA (Cam kết với khách hàng)

---

|Service|SLA|
|---|---|
|API Gateway|99.9% uptime|
|Order creation|99.9% success|
|Tracking realtime|99.5% availability|
|Dispatch (assign driver)|< 5s|
|Payment|99.5% success|

---

---

### Downtime allowed

99.9% → ~43 phút / tháng

---

---

## 5. 🎯 SLO (Internal Targets)

---

### 5.1 API Layer

|Metric|Target|
|---|---|
|Availability|99.95%|
|P95 latency|< 200ms|
|P99 latency|< 500ms|

---

---

### 5.2 Order Service

|Metric|Target|
|---|---|
|Create order latency|< 100ms|
|Success rate|99.99%|

---

---

### 5.3 Dispatch Flow

|Step|Target|
|---|---|
|Route calculation|< 2s|
|Optimization|< 5s|
|Driver assignment|< 5s total|

---

---

### 5.4 Kafka Processing

|Metric|Target|
|---|---|
|Consumer lag|< 1s|
|Event processing success|99.99%|

---

---

### 5.5 Tracking

|Metric|Target|
|---|---|
|Location update delay|< 2s|
|Realtime delivery|< 1s|

---

---

### 5.6 Payment

|Metric|Target|
|---|---|
|Processing time|< 3s|
|Success rate|99.5%|

---

---

## 6. 🧮 Error Budget

---

### Definition

Error Budget = 1 - SLO

---

---

### Example

SLO 99.9% → 0.1% error budget

---

---

### Usage

Nếu vượt error budget → stop release

---

---

## 7. 📉 Alerting Strategy

---

### Golden Signals

- Latency
- Traffic
- Errors
- Saturation

---

---

### Alert rules

---

#### Critical

Availability < 99%

---

---

#### Warning

Latency P95 > threshold

---

---

#### Kafka

Lag > 5s

---

---

## 8. 📊 Monitoring Stack

---

### Tools

- Prometheus
- Grafana

---

---

### Dashboards

- API latency
- Kafka lag
- Error rate
- Service health

---

---

## 9. 🔄 Incident Management

---

### Severity Levels

---

#### SEV-1

System down

---

---

#### SEV-2

Core flow degraded

---

---

#### SEV-3

Minor issue

---

---

### Response time

|Severity|Response|
|---|---|
|SEV-1|< 5 phút|
|SEV-2|< 15 phút|
|SEV-3|< 1 giờ|

---

---

## 10. 🔁 SLO Burn Rate

---

### Concept

Tốc độ tiêu hao error budget

---

---

### Example

Error spike → burn nhanh → alert sớm

---

---

## 11. ⚙️ SLO per User Journey

---

### Order flow

|Step|SLO|
|---|---|
|Create order|< 100ms|
|Assign driver|< 5s|
|Tracking|realtime|
|Delivery|success|

---

---

### Driver flow

|Step|SLO|
|---|---|
|Receive job|< 1s|
|Update status|< 200ms|

---

---

## 12. ⚠️ Anti-patterns

---

❌ Không define SLO  
❌ Chỉ đo uptime  
❌ Không có alert  
❌ Không dùng error budget  
❌ Alert quá nhiều (noise)

---

---

## 13. 🎯 Design Strategy

---

### Priority

1. Availability  
2. Latency  
3. Consistency

---

---

### Trade-off

Chấp nhận eventual consistency để đạt SLA

---

---

## 14. 🎯 Service Tiering

---

|Tier|Service|Requirement|
|---|---|---|
|Tier 0|API Gateway|99.99%|
|Tier 1|Order, Dispatch|99.95%|
|Tier 2|Tracking|99.9%|
|Tier 3|Analytics|best effort|

---

---

## 15. 📈 Capacity Planning

---

### Metrics

- RPS
- Kafka throughput
- DB QPS

---

---

### Strategy

Scale trước khi chạm SLO limit

---

---

## 16. 🎯 Design Guarantees

---

SLO/SLA đảm bảo:

- Hệ thống có mục tiêu rõ ràng
- Có thể đo lường
- Có thể cải thiện
- Tránh “cảm tính”

---

---

# ✅ Kết luận

SLO/SLA là:

> **“Hợp đồng giữa hệ thống và thực tế production”**

---

## Insight quan trọng nhất

> **Nếu bạn không đo — bạn không kiểm soát được**