## 1. Mục tiêu

Dispatch Context chịu trách nhiệm:

- Gom (batch) các order
- Quyết định phân bổ (assignment)
- Điều phối Routing + Optimization
- Xử lý re-dispatch khi có sự cố

---

## 2. Boundary & Responsibility

---

### Thuộc Dispatch

- Batch orders  
- Chọn vehicles  
- Gọi routing + optimizer  
- Assign driver  
- Re-optimize khi cần

---

### KHÔNG thuộc Dispatch

- Không tính toán đường đi (Routing)  
- Không solve VRP (Optimization)  
- Không lưu GPS (Tracking)

---

## 3. Ubiquitous Language

DispatchPlan  
Batch  
Assignment  
Constraint  
OptimizationRequest  
OptimizationResult

---

## 4. Aggregate Design

---

### Aggregate Root: `DispatchPlan`

DispatchPlan  
 ├── DispatchId  
 ├── Orders (List<OrderId>)  
 ├── Vehicles (List<VehicleId>)  
 ├── Status  
 ├── Constraints  
 ├── GeneratedRoutes  
 ├── CreatedAt  
 └── UpdatedAt

---

### Supporting Entities

Assignment  
 ├── OrderId  
 ├── VehicleId  
 ├── Sequence

---

### Value Objects

Constraint  
 ├── MaxCapacity  
 ├── TimeWindow  
 ├── MaxDistance

---

## 5. Dispatch State Machine (CRITICAL)

---

CREATED  
  ↓  
BATCHING  
  ↓  
ROUTING_REQUESTED  
  ↓  
OPTIMIZATION_REQUESTED  
  ↓  
ASSIGNING  
  ↓  
COMPLETED  
  ↓  
MONITORING

---

### Failure Branch

OPTIMIZATION_FAILED → RETRYING → FAILED  
ASSIGNMENT_FAILED → REPLANNING

---

## 6. State Transition Rules

---

### CREATED → BATCHING

- Có order mới  
- Chưa đủ batch

---

### BATCHING → ROUTING_REQUESTED

- Batch đạt threshold (size hoặc time)

---

### ROUTING_REQUESTED → OPTIMIZATION_REQUESTED

- Distance matrix đã sẵn sàng

---

### OPTIMIZATION_REQUESTED → ASSIGNING

- Nhận OptimizationResult hợp lệ

---

### ASSIGNING → COMPLETED

- Tất cả order đã assign

---

### COMPLETED → MONITORING

- Bắt đầu tracking delivery

---

## 7. Batching Strategy (RẤT QUAN TRỌNG)

---

### 7.1 Time-based batching

Batch mỗi 30s – 2 phút

---

### 7.2 Size-based batching

Batch khi đạt N orders (vd: 20–50)

---

### 7.3 Hybrid (khuyến nghị)

Trigger khi:  
- đủ size  
HOẶC  
- hết timeout

---

### Trade-off

|Strategy|Ưu|Nhược|
|---|---|---|
|Time|ổn định|chậm|
|Size|tối ưu|delay|
|Hybrid|cân bằng|phức tạp|

---

## 8. Constraint Model

---

### Input constraints

- Vehicle capacity  
- Time window  
- Driver availability  
- Max route distance

---

### Rule

Dispatch validate constraint trước khi gọi optimizer

---

---

## 9. Saga Orchestration

---

### Flow (simplified)

OrderReadyForDispatch  
   ↓  
DispatchPlanCreated  
   ↓  
RouteRequested  
   ↓  
RouteGenerated  
   ↓  
OptimizationRequested  
   ↓  
OptimizationCompleted  
   ↓  
DriverAssigned

---

### Saga Responsibility

- Track state  
- Retry khi fail  
- Trigger compensation

---

## 10. Events

---

### Publish

DispatchPlanCreated  
RouteRequested  
OptimizationRequested  
DriverAssigned  
DispatchCompleted  
DispatchFailed

---

### Consume

OrderReadyForDispatch  
DriverAvailable  
RouteGenerated  
OptimizationCompleted  
OptimizationFailed

---

---

## 11. Re-Optimization Strategy

---

### Trigger conditions

- Driver reject job  
- Traffic thay đổi lớn  
- Vehicle breakdown

---

### Flow

DispatchPlan  
   ↓  
Mark affected orders  
   ↓  
Re-run optimization  
   ↓  
Re-assign

---

### Rule

Không re-optimize toàn bộ nếu không cần  
→ chỉ subset

---

## 12. Failure Handling

---

### Case 1: Routing fail

→ retry 3 lần  
→ fallback (distance heuristic)

---

### Case 2: Optimization fail

→ retry  
→ fallback greedy assignment

---

### Case 3: No available driver

→ delay dispatch  
→ retry sau X phút

---

---

## 13. Domain Service (Core Logic)

---

public class DispatchDomainService  
{  
    public DispatchPlan CreatePlan(List<Order> orders, List<Vehicle> vehicles)  
    {  
        // validate  
        // batching logic  
        // constraint check  
    }  
  
    public void AssignRoutes(DispatchPlan plan, OptimizationResult result)  
    {  
        // mapping result → assignment  
    }  
}

---

---

## 14. Integration Points

---

### Routing Context

- Input: locations
- Output: distance matrix

---

### Optimization Context

- Input: matrix + constraints
- Output: routes

---

---

## 15. Persistence Model

---

### MySQL

DispatchPlans  
Assignments

---

### MongoDB

SagaState  
DispatchLogs

---

---

## 16. Anti-patterns

---

Dispatch tự tính route  
Dispatch nhét logic optimizer  
Batch quá lớn (timeout cao)  
Re-optimize toàn bộ liên tục

---

---

## 17. Design Guarantees

---

Dispatch Context đảm bảo:

- Assignment tối ưu (qua optimizer)
- Có thể retry & recover
- Không coupling compute logic
- Scale độc lập