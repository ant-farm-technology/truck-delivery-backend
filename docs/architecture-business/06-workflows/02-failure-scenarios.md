## 1. Mục tiêu

Tài liệu này định nghĩa:

- Các failure có thể xảy ra
- Ảnh hưởng tới hệ thống
- Cách xử lý & recovery
- Strategy để tránh cascading failure

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Failure là default, không phải exception

Network fail  
Service down  
Timeout  
Duplicate event  
→ tất cả đều sẽ xảy ra

---

### 2.2 Không có “exactly-once”

System = at-least-once  
→ phải chịu duplicate

---

### 2.3 Graceful degradation

Không crash toàn hệ  
→ degrade từng phần

---

---

## 3. Category 1: Dispatch Failures

---

### 3.1 Không có driver phù hợp

---

#### Cause

- Không đủ capacity  
- Không có driver nearby

---

#### Detection

OptimizationCompleted → no assignment

---

#### Handling

- Retry sau 30–60s  
- Relax constraint  
- Escalate (manual)

---

---

### 3.2 Routing Service fail

---

#### Cause

- Service down
- PostGIS lỗi

---

#### Handling

Fallback:  
→ dùng Haversine distance (approx)

---

---

### 3.3 Optimization timeout

---

#### Cause

- Input lớn
- Solver quá lâu (Google OR-Tools)

---

#### Handling

- Timeout (5–10s)  
- Return best solution so far  
- Fallback greedy

---

---

## 4. Category 2: Fleet Failures

---

### 4.1 Race condition assign driver

---

#### Scenario

2 Dispatch assign cùng 1 driver

---

#### Handling

- Optimistic locking  
- Check status trước khi update

---

---

### 4.2 Driver mất kết nối

---

#### Detection

No heartbeat > 60s

---

#### Handling

- Mark OFFLINE  
- Trigger re-dispatch

---

---

## 5. Category 3: Tracking Failures

---

### 5.1 GPS noise / sai vị trí

---

#### Handling

- Map matching  
- Ignore abnormal jump

---

---

### 5.2 Traffic spike (10k+ drivers)

---

#### Problem

50k events/sec

---

#### Handling

- Rate limit  
- Drop duplicate points  
- Backpressure queue

---

---

### 5.3 Out-of-order events

---

#### Handling

Sort theo timestamp

---

---

## 6. Category 4: Payment Failures

---

### 6.1 Gateway timeout

---

#### Handling

- Retry (idempotent)  
- Check status via polling

---

---

### 6.2 Double charge risk

---

#### Cause

Retry request

---

#### Handling

IdempotencyKey

---

---

### 6.3 Webhook missing

---

#### Handling

- Polling fallback  
- Reconciliation job

---

---

## 7. Category 5: Event System Failures

---

### 7.1 Duplicate events

---

#### Cause

Kafka retry (:contentReference[oaicite:1]{index=1})

---

#### Handling

Consumer idempotency

---

---

### 7.2 Event ordering sai

---

#### Handling

Partition theo key (orderId)

---

---

### 7.3 Consumer down

---

#### Handling

- Kafka retain message  
- Consumer resume

---

---

## 8. Category 6: Saga Failures

---

### 8.1 Step fail giữa chừng

---

#### Handling

Compensation step

---

---

### 8.2 Saga stuck

---

#### Cause

Event missing

---

#### Handling

- Timeout detection  
- Saga watchdog job

---

---

## 9. Category 7: API Failures

---

### 9.1 Client retry

---

#### Handling

Idempotency-Key header

---

---

### 9.2 Invalid input

---

#### Handling

Validation tại API layer

---

---

## 10. Category 8: Infrastructure Failures

---

### 10.1 Redis down

---

#### Impact

- Cache miss  
- Idempotency ảnh hưởng

---

#### Handling

- Fallback DB  
- Degrade performance

---

---

### 10.2 Database down

---

#### Handling

- Circuit breaker  
- Retry with backoff

---

---

### 10.3 Kafka cluster issue

---

#### Handling

- Retry publish  
- Buffer event (outbox)

---

---

## 11. Global Resilience Patterns

---

### Circuit Breaker

Fail fast khi downstream lỗi

---

### Retry

Exponential backoff

---

### Timeout

Không block lâu

---

### Bulkhead

Isolate service failure

---

---

## 12. Observability

---

### Metrics quan trọng

- Error rate  
- Retry count  
- Timeout count  
- Saga failure rate

---

### Logging

- correlationId  
- eventId

---

---

## 13. Anti-patterns

---

Assume success  
Không retry  
Retry vô hạn  
Không timeout  
Không idempotency  
Không monitoring

---

---

## 14. Design Guarantees

---

Failure Handling đảm bảo:

- System không crash dây chuyền
- Có thể recover
- Có thể debug
- User vẫn có trải nghiệm ổn