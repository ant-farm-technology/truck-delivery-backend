## 1. Mục tiêu

Fleet Context chịu trách nhiệm:

- Quản lý **tài xế (Driver)** và **phương tiện (Vehicle)**
- Quản lý **trạng thái sẵn sàng (availability)**
- Cung cấp thông tin tài nguyên cho Dispatch

---

## 2. Boundary & Responsibility

---

### Thuộc Fleet Context

- Quản lý Driver lifecycle  
- Quản lý Vehicle lifecycle  
- Tracking trạng thái (Idle, Busy, Offline)  
- Quản lý capacity (weight, volume)

---

### KHÔNG thuộc Fleet Context

- Không assign order  
- Không quyết định route  
- Không tối ưu tuyến  
- Không batch order

Những việc này thuộc **Dispatch Context**

---

## 3. Ubiquitous Language

Driver  
Vehicle  
Capacity  
Availability  
AssignmentStatus  
FleetStatus

---

## 4. Aggregate Design

---

## 4.1 Aggregate Root: `Driver`

Driver  
 ├── DriverId  
 ├── Status (Offline, Idle, Busy)  
 ├── VehicleId (nullable)  
 ├── CurrentLocation (VO)  
 ├── LastHeartbeat  
 └── CreatedAt

---

## 4.2 Aggregate Root: `Vehicle`

Vehicle  
 ├── VehicleId  
 ├── Capacity (VO)  
 ├── Status (Available, InUse, Maintenance)  
 ├── DriverId (nullable)  
 └── CreatedAt

---

### Lưu ý quan trọng

Driver và Vehicle là 2 aggregate riêng biệt  
→ không transaction cross-aggregate

---

## 5. Value Objects

---

### Capacity

public record Capacity(double Weight, double Volume);

---

### Location

public record Location(double Latitude, double Longitude);

---

---

## 6. Driver State Machine

---

OFFLINE  
  ↓  
IDLE  
  ↓  
BUSY  
  ↓  
IDLE

---

### Rules

OFFLINE → IDLE  
 → Driver online  
  
IDLE → BUSY  
 → Khi nhận assignment (từ Dispatch)  
  
BUSY → IDLE  
 → Khi hoàn thành delivery

---

### Invalid Transitions

OFFLINE → BUSY  
BUSY → OFFLINE (không clean state)

---

---

## 7. Vehicle State Machine

---

AVAILABLE  
  ↓  
IN_USE  
  ↓  
AVAILABLE

---

### Rules

AVAILABLE → IN_USE  
 → Khi gán cho driver  
  
IN_USE → AVAILABLE  
 → Khi driver hoàn tất

---

---

## 8.  Driver ↔ Vehicle Association

---

### Quy tắc

- 1 Driver ↔ 1 Vehicle (tại một thời điểm)  
- Có thể thay đổi theo thời gian

---

### Consistency Strategy

Không dùng distributed transaction.

Driver.AssignVehicle(vehicleId)  
Vehicle.AssignDriver(driverId)

→ eventual consistency qua event

---

---

## 9. Domain Events

---

### Publish

DriverRegistered  
DriverAvailable  
DriverBusy  
DriverOffline  
VehicleRegistered  
VehicleAvailable  
VehicleInUse

---

### Consume

DriverAssigned (từ Dispatch)  
DeliveryCompleted (từ Order/Tracking)

---

---

## 10. Availability Model (QUAN TRỌNG)

---

### Driver Available khi:

- Status = IDLE  
- Có Vehicle  
- Không vi phạm constraint

---

### Vehicle Available khi:

- Status = AVAILABLE  
- Không bị maintenance

---

### Fleet Availability = intersection

AvailableDriver ∩ AvailableVehicle

---

---

## 11. Capacity Model

---

### Capacity check KHÔNG nằm ở Dispatch hoàn toàn

Fleet phải expose:

CanHandle(package)

---

### Ví dụ:

public bool CanHandle(Package package)  
{  
    return package.Weight <= Capacity.Weight &&  
           package.Volume <= Capacity.Volume;  
}

---

---

## 12. Location & Heartbeat

---

### Driver gửi heartbeat

POST /drivers/{id}/heartbeat

---

### Update

- CurrentLocation  
- LastHeartbeat

---

### Rule

Nếu timeout (vd: 60s) → Driver = OFFLINE

---

---

## 13. Integration Points

---

### Dispatch Context

- Nhận:

DriverAvailable  
VehicleAvailable

- Gửi:

DriverAssigned

---

### Tracking Context

- Gửi location updates

---

---

## 14. Persistence Model

---

### MySQL Tables

Drivers  
Vehicles  
DriverVehicleAssignments

---

---

## 15. Consistency & Concurrency

---

### Race condition phổ biến

2 Dispatch assign cùng 1 driver

---

### Giải pháp

- Optimistic locking  
- Status check trước khi assign

---

---

## 16. Anti-patterns

---

Fleet tự assign order  
Fleet biết route  
Fleet gọi optimizer  
Driver chứa list orders

---

---

## 17. Design Guarantees

---

Fleet Context đảm bảo:

- Trạng thái driver luôn nhất quán
- Capacity luôn đúng
- Dispatch có dữ liệu chính xác để quyết định