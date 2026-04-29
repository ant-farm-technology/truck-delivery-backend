## 1. Mục tiêu

Tài liệu này xác định:

- Ranh giới domain (bounded contexts)
- Ownership của business logic
- Cách các context tương tác với nhau

---

## 2. Nguyên tắc phân tách context

---

### 2.1 Tách theo “quyền ra quyết định” (Decision Authority)

Nếu 2 logic cần deploy / scale độc lập → tách context  
Nếu 2 logic thay đổi cùng nhau → cùng context

---

### 2.2 Không tách theo:

Bảng database  
Service hiện tại  
Team structure (ban đầu)

---

### 2.3 Rule quan trọng

1 Bounded Context = 1 Ubiquitous Language

---

## 3. Danh sách Bounded Contexts

Hệ thống chia thành 6 contexts:

1. Identity Context  
2. Order Context  
3. Fleet Context  
4. Dispatch Context (CORE)  
5. Routing Context  
6. Tracking Context  
7. Payment Context (supporting)

---

## 4. Context Classification (DDD)

---

|Context|Type|
|---|---|
|Dispatch|Core Domain|
|Order|Supporting|
|Fleet|Supporting|
|Routing|Generic|
|Optimization|Generic|
|Tracking|Supporting|
|Identity|Generic|
|Payment|Supporting|

---

Insight quan trọng:

Dispatch = nơi tạo business value chính

## 5. Context Definitions

---

### 5.1 Identity Context

#### Responsibility

- Authentication
- Authorization

#### Boundary

Không chứa business logic domain

---

---

### 5.2 Order Context

#### Responsibility

- Quản lý vòng đời đơn hàng

#### Ubiquitous Language

Order, Pickup, Delivery, Status, TimeWindow

---

#### Không làm:

Không assign driver  
Không tính route

---

---

### 5.3 Fleet Context

#### Responsibility

- Quản lý xe và tài xế

#### Ubiquitous Language

Driver, Vehicle, Capacity, Availability

---

#### Không làm:

Không quyết định route  
Không xử lý đơn hàng

---

---

### 5.4 Dispatch Context (CORE)

---

#### Responsibility

- Gom đơn hàng
- Quyết định assign
- Điều phối routing + optimization

---

#### Ubiquitous Language

DispatchPlan, Assignment, Batch, Constraint

---

#### Decision Authority

Order nào được gom chung  
Xe nào được assign  
Khi nào cần re-optimize

---

#### Không làm:

Không tính toán route (delegated)  
Không lưu GPS

---

---

### 5.5 Routing Context

---

#### Responsibility

- Tính toán đường đi
- Distance matrix

---

#### Data Source

- OpenStreetMap
- PostGIS

---

#### Rule

Stateless + deterministic

---

---

### 5.6 Optimization Context

---

#### Responsibility

- Giải bài toán Vehicle Routing Problem

---

#### Tool

- Google OR-Tools

---

#### Rule

Không chứa business logic

---

---

### 5.7 Tracking Context

---

#### Responsibility

- Theo dõi vị trí realtime

---

#### Ubiquitous Language

Location, TrackingSession, RouteProgress

---

---

### 5.8 Payment Context

---

#### Responsibility

- Tính phí
- Thanh toán

## 6. 🔗 Context Map

---

Order ──→ Dispatch ──→ Routing ──→ Optimization  
   │           │  
   │           └────→ Fleet  
   │  
   └────→ Payment  
  
Tracking (song song)  
Identity (global)

---

## 7. Integration Patterns

---

|From|To|Pattern|
|---|---|---|
|Order → Dispatch|Event||
|Dispatch → Routing|Sync (HTTP/gRPC)||
|Dispatch → Optimization|Sync||
|Dispatch → Fleet|Command/Event||
|Dispatch → Order|Event||
|Tracking → Dispatch|Event||

---

---

## 8. Anti-Corruption Layer (ACL)

---

### Tại sao cần?

Mỗi context có model riêng

---

### Ví dụ

Dispatch → Optimization:

DispatchPlan → OptimizationRequestDTO

---

### Rule

Không dùng trực tiếp entity giữa context  
Luôn map qua DTO

---

---

## 9. Data Ownership

---

|Context|Database|
|---|---|
|Identity|MySQL|
|Order|MySQL|
|Fleet|MySQL|
|Dispatch|MySQL + MongoDB|
|Routing|PostGIS|
|Tracking|MongoDB|
|Payment|MySQL|

---

---

## 10. Boundary Rules (BẮT BUỘC)

---

1. Không context nào truy cập DB của context khác  
2. Không share entity giữa context  
3. Mọi integration phải qua event hoặc API  
4. Compute context phải stateless  
5. Dispatch không được bypass Routing/Optimization

---

---

## 11. Common Boundary Violations

---

Order chứa driverId  
Fleet quyết định route  
Dispatch tự tính toán distance  
Python service truy cập MySQL

---

---

## 12. Evolution Strategy

---

### Phase 1 (MVP)

- Gộp Order + Dispatch

---

### Phase 2

- Tách Dispatch riêng

---

### Phase 3

- Tách Routing + Optimization