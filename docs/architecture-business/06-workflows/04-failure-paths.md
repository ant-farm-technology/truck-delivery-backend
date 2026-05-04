## 1. 🎯 Mục tiêu

Tài liệu này định nghĩa:

- Các failure scenarios quan trọng
- Cách hệ thống phản ứng
- Cơ chế retry / fallback / compensation
- Đảm bảo system không “gãy dây chuyền”

---

## 2. 🧠 Nguyên tắc xử lý lỗi

---

### 2.1 Fail isolated

1 service fail ≠ toàn hệ fail

---

### 2.2 Retry có kiểm soát

Retry + backoff + limit

---

### 2.3 Idempotency bắt buộc

Duplicate event ≠ duplicate effect

---

---

### 2.4 Eventual consistency

Không cần đúng ngay lập tức

---

---

## 3. 🚨 Failure Scenarios (Core)

---

# 3.1 ❌ Order tạo xong nhưng không publish được event

---

## 📍 Root cause

- Kafka tạm thời unavailable
- Network issue
- Producer fail

---

## 💥 Impact

Order tồn tại nhưng không được dispatch

---

## ✅ Solution

---

### Pattern: Outbox

DB commit → Outbox → background publish → Kafka

---

### Retry

Retry until success

---

### Guarantee

Không mất event

---

---

# 3.2 ❌ Kafka message bị duplicate

---

## 📍 Root cause

- Retry producer
- Consumer crash trước commit offset

---

## 💥 Impact

Driver bị assign 2 lần

---

## ✅ Solution

---

### Idempotency key

eventId / orderId

---

### Storage

- Redis / DB

---

### Rule

Processed → skip

---

---

# 3.3 ❌ Routing Service fail

---

## 📍 Root cause

- Rust service crash
- PostGIS query fail

---

## 💥 Impact

Không tính được route → không dispatch

---

## ✅ Solution

---

### Retry (async)

Kafka consumer retry

---

---

### Fallback

Use simple distance (Haversine)

---

---

### DLQ

routing.events.dlq

---

---

# 3.4 ❌ Optimization (OR-Tools) fail / timeout

---

## 📍 Root cause

- Solver quá nặng
- Timeout
- Input invalid

---

## 💥 Impact

Không assign được driver

---

## ✅ Solution

---

### Fallback strategy

Greedy assignment (nearest driver)

---

---

### Timeout guard

Max solve time = 2–5s

---

---

### Emit event

DriverAssignmentFallback

---

---

# 3.5 ❌ Không có driver available

---

## 📍 Root cause

- Peak hour
- Fleet exhausted

---

## 💥 Impact

Order stuck ở CREATED

---

## ✅ Solution

---

### Retry loop

Re-dispatch sau X giây

---

---

### State

CREATED → PENDING_ASSIGNMENT

---

---

### Notification

Inform customer delay

---

---

# 3.6 ❌ Driver nhận job nhưng không phản hồi

---

## 📍 Root cause

- App offline
- Driver ignore

---

## 💥 Impact

Order bị delay

---

## ✅ Solution

---

### Timeout

30–60s

---

---

### Reassign

Driver khác

---

---

### Saga compensation

DriverAssigned → rollback

---

---

# 3.7 ❌ Tracking mất dữ liệu (driver offline)

---

## 📍 Root cause

- Network yếu
- App crash

---

## 💥 Impact

Customer không thấy realtime

---

## ✅ Solution

---

### Strategy

- Buffer local (driver app)  
- Gửi lại khi online

---

---

### UI fallback

Last known location

---

---

# 3.8 ❌ Payment fail

---

## 📍 Root cause

- External gateway fail
- Card decline

---

## 💥 Impact

Order delivered nhưng chưa thanh toán

---

## ✅ Solution

---

### Retry

Retry payment

---

---

### State

DELIVERED → PAYMENT_PENDING

---

---

### Compensation

Manual / retry later

---

---

# 3.9 ❌ Kafka consumer lag cao

---

## 📍 Root cause

- Traffic spike
- Consumer scale chưa đủ

---

## 💥 Impact

Delay toàn hệ

---

## ✅ Solution

---

### Scale consumer

↑ instances

---

---

### Partition tuning

↑ partitions

---

---

### Monitoring

- Lag alert

---

---

# 3.10 ❌ Service crash (Order / Dispatch / Tracking)

---

## 📍 Root cause

- Bug
- OOM

---

## 💥 Impact

Service unavailable

---

## ✅ Solution

---

### Kubernetes restart

Auto restart pod

---

---

### Stateless design

Không mất state

---

---

### Replay Kafka

Re-process events

---

---

# 3.11 ❌ Database fail

---

## 📍 Root cause

- MySQL down
- Connection pool exhausted

---

## 💥 Impact

Write fail

---

## ✅ Solution

---

### Retry

Exponential backoff

---

---

### Circuit breaker

Stop flood

---

---

### Fallback

Queue request (Kafka)

---

---

# 3.12 ❌ Redis fail (cache / idempotency)

---

## 📍 Root cause

- Redis crash

---

## 💥 Impact

Idempotency có thể fail

---

## ✅ Solution

---

### Fallback

Check DB

---

---

### Rule

Redis = optimization, không critical

---

---

## 4. 🔄 Saga Failure Handling

---

### Pattern

Step fail → compensation

---

---

### Example

DriverAssigned  
  ↓  
Fleet update fail  
  ↓  
Rollback assignment

---

---

## 5. 🧪 Retry Strategy

---

### Backoff

1s → 2s → 5s → 10s

---

---

### Limit

max 5–10 retries

---

---

### After fail

→ DLQ

---

---

## 6. 📦 Dead Letter Queue (DLQ)

---

### Purpose

Không mất event khi fail

---

---

### Topics

*.dlq

---

---

### Handling

Manual / automated reprocess

---

---

## 7. 📊 Observability cho Failure

---

### Detect

- Error rate
- Kafka lag
- Timeout

---

---

### Tools

- Prometheus
- Grafana

---

---

### Debug flow

CorrelationId → Trace → Logs → Metrics

---

---

## 8. ⚠️ Anti-patterns

---

❌ Không retry  
❌ Retry vô hạn  
❌ Không idempotency  
❌ Không DLQ  
❌ Fail = crash service  
❌ Sync chain dài

---

---

## 9. 🎯 Design Guarantees

---

Failure handling đảm bảo:

- Không mất dữ liệu
- Không cascade failure
- Recover được
- System degrade gracefully

---

---

# ✅ Kết luận

Failure path không phải là “edge case”

> **Nó là trạng thái mặc định của production**

---

## Insight quan trọng nhất

> **Bạn không build system để chạy khi mọi thứ đúng — bạn build để survive khi mọi thứ sai**