## 1. Mục tiêu

Optimization Context chịu trách nhiệm:

- Giải bài toán **Vehicle Routing Problem (VRP)**
- Tối ưu:
    - Quãng đường
    - Thời gian
    - Sử dụng tài nguyên (vehicle)

---

## 2. Boundary & Responsibility

---

### Thuộc Optimization

- Solve VRP  
- Apply constraints  
- Generate route plan

---

### KHÔNG thuộc Optimization

- Không lấy data trực tiếp từ DB  
- Không quyết định business rule  
- Không lưu state

Stateless compute service

---

## 3. Ubiquitous Language

VehicleRoutingProblem (VRP)  
Node  
Depot  
Route  
Cost  
Constraint  
Solution

---

## 4. Domain Model

---

### 4.1 OptimizationRequest

OptimizationRequest  
 ├── Orders  
 ├── Vehicles  
 ├── DistanceMatrix  
 ├── Constraints

---

### 4.2 OptimizationResult

OptimizationResult  
 ├── Routes  
 ├── TotalCost  
 ├── UnassignedOrders

---

### 4.3 Route

Route  
 ├── VehicleId  
 ├── Stops (ordered)  
 ├── TotalDistance  
 ├── TotalTime

---

---

## 5. Problem Types

---

### 5.1 CVRP (Capacitated VRP)

Giới hạn tải trọng

---

### 5.2 VRPTW (Time Window)

Mỗi order có khoảng thời gian giao

---

### 5.3 Multi-Depot (tuỳ chọn)

Nhiều điểm xuất phát

---

---

## 6. Constraint Model

---

### Hard Constraints (bắt buộc)

- Capacity (weight, volume)  
- Time window  
- Vehicle availability

---

### Soft Constraints (có thể vi phạm)

- Ưu tiên giao sớm  
- Giảm số xe sử dụng

---

---

## 7. Cost Function (CRITICAL)

---

### Mục tiêu tối ưu:

Minimize:  
- Total distance  
- Total time  
- Penalty (vi phạm constraint)

---

### Ví dụ:

Cost = Distance * W1 + Time * W2 + Penalty * W3

---

### Rule

Cost function phải rõ ràng, không “magic”

---

---

## 8. OR-Tools Modeling

---

### Core Components

- RoutingIndexManager
- RoutingModel
- TransitCallback
- Dimension (time, capacity)

---

### Flow

1. Load data  
2. Create routing model  
3. Add constraints  
4. Solve  
5. Extract solution

---

---

## 9. API Design

---

### Endpoint

POST /optimize

---

### Input

{  
  "orders": [],  
  "vehicles": [],  
  "distanceMatrix": [],  
  "constraints": {}  
}

---

### Output

{  
  "routes": [],  
  "totalCost": 0,  
  "unassignedOrders": []  
}

---

---

## 10. Performance & Scaling

---

### 10.1 Bottleneck

Solver time tăng nhanh theo N

---

### 10.2 Strategy

---

#### Limit input size

Max 50–100 orders / batch

---

#### Timeout

5–10 seconds max

---

#### Parallel jobs

Queue-based processing

---

---

## 11. Fallback Strategy

---

### Khi solver fail:

→ Greedy assignment

---

### Ví dụ:

- Gán order gần nhất cho xe gần nhất

---

---

## 12. Integration Points

---

### Input từ Dispatch

- Orders  
- Vehicles  
- Matrix

---

### Output về Dispatch

- Route plan

---

---

## 13. Determinism & Reproducibility

---

Same input → same output (nếu seed cố định)

---

Khuyến nghị:

Set random seed

---

---

## 14. Observability

---

### Metrics

- Solve time  
- Cost  
- Unassigned orders

---

### Logging

- Input size  
- Constraint violations

---

---

## 15. Failure Handling

---

### Case 1: No solution

→ return partial solution  
→ mark unassigned orders

---

### Case 2: Timeout

→ return best solution so far

---

---

## 16. Anti-patterns

---

Gọi OR-Tools trực tiếp từ .NET  
Không giới hạn batch size  
Không timeout  
Không fallback  
Hardcode cost function

---

---

## 17. Design Guarantees

---

Optimization Context đảm bảo:

- Tối ưu hoá hiệu quả
- Không làm block hệ thống
- Có fallback khi fail
- Có thể scale độc lập