# /debug-saga — Debug & Analyze Saga State

Debug trạng thái của một Choreography Saga trong hệ thống truck delivery.

**Arguments:** `$ARGUMENTS` = `{SagaId hoặc OrderId}` (UUID)

## Yêu cầu

Khi người dùng cung cấp một ID, hãy:

### 1. Tìm Saga State trong MongoDB

```javascript
// Kết nối MongoDB và tìm SagaState
db.ShipmentSagaStates.findOne({
  $or: [
    { _id: UUID("$ARGUMENTS") },
    { OrderId: UUID("$ARGUMENTS") }
  ]
})
```

Hiển thị thông tin:
- `Status` hiện tại
- `CompletedSteps` đã hoàn thành
- `RetryCount`
- `FailureReason` (nếu có)
- `StartedAt`, `CompletedAt`, `FailedAt`

### 2. Trace Event Chain

Dựa vào Saga flow chuẩn, xác định bước đang bị stuck:

```
EXPECTED FLOW:
Step 1: OrderCreatedEvent received       → Status: Created
Step 2: Route calculated (Route Service) → Status: RoutePlanning
Step 3: Optimizer returned result        → Status: DriverAssigning
Step 4: DriverAssignmentRequested        → Status: DriverAssigning
Step 5: DriverAssigned received          → Status: DriverConfirmed
Step 6: ShipmentStarted published        → Status: InProgress
```

**So sánh CompletedSteps với expected flow** để xác định:
- Bước cuối cùng hoàn thành là bước nào?
- Bước nào bị thiếu?
- Failure xảy ra ở đâu?

### 3. Kiểm tra Kafka Consumer Lag

```bash
# Check consumer lag cho các topics liên quan
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --describe --group shipment-service-group

# Check DLQ
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic order.order.created.dlq --from-beginning --max-messages 10
```

### 4. Kiểm tra Logs

```
# Tìm logs theo correlationId / sagaId
grep -r "$ARGUMENTS" /var/log/services/ | grep -E "(ERROR|WARN)"

# Hoặc nếu dùng Grafana Loki:
{service="shipment-service"} |= "$ARGUMENTS"
```

### 5. Phân tích nguyên nhân và đề xuất fix

Dựa vào trạng thái, đề xuất:

**Case: Status = RoutePlanning, RetryCount > 0**
```
→ Route Service có thể đang down hoặc timeout
→ Kiểm tra: GET http://route-service:8084/health
→ Fix: Restart saga từ step Route calculation
```

**Case: Status = DriverAssigning, RetryCount >= 5**
```
→ Không tìm được driver phù hợp
→ Kiểm tra: GET /api/v1/drivers/available
→ Fix: Relax constraints, hoặc manual assignment
→ Command: Order.UpdateStatus(Pending) để retry
```

**Case: Status = Failed, FailureReason có**
```
→ Đọc FailureReason
→ Xác định compensating transactions đã chạy chưa
→ Kiểm tra Order status = Pending (đã compensate)
→ Kiểm tra Driver status = Available (đã compensate)
```

**Case: Saga stuck (no update > 10 min)**
```
→ Có thể event bị lost
→ Kiểm tra Kafka topic offset
→ Manual republish event nếu cần
→ Hoặc trigger saga watchdog job
```

### 6. Manual Recovery Commands

Nếu cần recover thủ công:

```csharp
// Reset saga để retry từ đầu
POST /api/v1/admin/sagas/{sagaId}/reset
{
  "action": "retry",
  "fromStep": "RouteCalculation"
}

// Force complete (emergency)
POST /api/v1/admin/sagas/{sagaId}/force-assign
{
  "driverId": "uuid",
  "vehicleId": "uuid"
}

// Cancel saga + compensate
POST /api/v1/admin/sagas/{sagaId}/cancel
```

### 7. Báo cáo

Tổng hợp thành báo cáo:

```
SAGA DIAGNOSIS REPORT
=====================
Saga ID:    {id}
Order ID:   {orderId}
Status:     {status}
Duration:   {startedAt} → {now} ({minutes} min)

COMPLETED STEPS: {n}/{total}
✅ Step 1: OrderReceived
✅ Step 2: RouteCalculated  
❌ Step 3: DriverAssigning (FAILED - reason: {reason})
⏳ Step 4: DriverConfirmed (PENDING)

ROOT CAUSE: {analysis}

RECOMMENDED ACTION: {action}
```

## Rules

- Đây là READ-ONLY investigation tool — không modify data trực tiếp
- Mọi recovery action phải confirm với user trước khi thực hiện
- Log đầy đủ correlationId khi debug
- Kiểm tra cả MongoDB saga state VÀ Kafka consumer lag
