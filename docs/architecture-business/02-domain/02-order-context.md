## 1. Mục tiêu

Order Context chịu trách nhiệm:

- Quản lý vòng đời đơn hàng
- Validate business rules liên quan đến order
- Publish domain events

---

## 2. Scope & Boundary

---

### Thuộc Order Context

- Tạo đơn hàng  
- Validate dữ liệu (location, time window)  
- Quản lý trạng thái Order

---

### KHÔNG thuộc Order Context

- Assign driver  
- Tính toán route  
- Tối ưu tuyến  
- Tracking GPS

Tất cả thuộc về **Dispatch / Routing / Tracking**

---

## 3. Ubiquitous Language

Order  
PickupLocation  
DeliveryLocation  
TimeWindow  
Package  
OrderStatus

---

## 4. Aggregate Design

---

### Aggregate Root: `Order`

Order  
 ├── OrderId  
 ├── PickupLocation (VO)  
 ├── DeliveryLocation (VO)  
 ├── Package (VO)  
 ├── TimeWindow (VO)  
 ├── Status  
 ├── CreatedAt  
 └── UpdatedAt

---

### Value Objects

---

#### Location

public record Location(double Latitude, double Longitude);

---

#### TimeWindow

public record TimeWindow(DateTime Start, DateTime End);

---

#### Package

public record Package(double Weight, double Volume);

---

## 5. Order State Machine (QUAN TRỌNG)

---

CREATED  
  ↓  
READY_FOR_DISPATCH  
  ↓  
ASSIGNED  
  ↓  
PICKED_UP  
  ↓  
IN_TRANSIT  
  ↓  
DELIVERED

---

### State Definitions

|State|Ý nghĩa|
|---|---|
|CREATED|vừa tạo|
|READY_FOR_DISPATCH|đủ điều kiện để dispatch|
|ASSIGNED|đã có driver|
|PICKED_UP|đã lấy hàng|
|IN_TRANSIT|đang giao|
|DELIVERED|hoàn tất|

---

### State Transition Rules

CREATED → READY_FOR_DISPATCH  
 → validate location + time window  
  
READY_FOR_DISPATCH → ASSIGNED  
 → chỉ qua event DriverAssigned  
  
ASSIGNED → PICKED_UP  
 → driver xác nhận  
  
PICKED_UP → IN_TRANSIT  
 → bắt đầu di chuyển  
  
IN_TRANSIT → DELIVERED  
 → hoàn tất

---

### Invalid Transitions (phải reject)

CREATED → ASSIGNED  
READY_FOR_DISPATCH → DELIVERED  
DELIVERED → bất kỳ state nào khác

---

## 6. Domain Events

---

### OrderCreated

{  
  "eventType": "order.order.created.v1",  
  "payload": {  
    "orderId": "uuid",  
    "pickupLocation": {},  
    "deliveryLocation": {},  
    "timeWindow": {},  
    "package": {}  
  }  
}

---

### OrderReadyForDispatch

{  
  "eventType": "order.order.ready_for_dispatch.v1",  
  "payload": {  
    "orderId": "uuid"  
  }  
}

---

### DriverAssigned (consume)

{  
  "eventType": "driver.driver.assigned.v1",  
  "payload": {  
    "orderId": "uuid",  
    "driverId": "uuid"  
  }  
}

---

### OrderDelivered

{  
  "eventType": "order.order.delivered.v1",  
  "payload": {  
    "orderId": "uuid"  
  }  
}

---

## 7. Business Rules

---

### 7.1 Order Creation

- Pickup != Delivery  
- TimeWindow hợp lệ  
- Package > 0

---

### 7.2 Ready for Dispatch

- Có đủ thông tin  
- Không bị cancel

---

### 7.3 Assignment

- Không tự assign  
- Chỉ nhận từ Dispatch event

---

## 8. Domain Methods (C# sample)

---

public class Order  
{  
    public void MarkReadyForDispatch()  
    {  
        if (!IsValidForDispatch())  
            throw new DomainException("Invalid order");  
  
        Status = OrderStatus.ReadyForDispatch;  
    }  
  
    public void AssignDriver(Guid driverId)  
    {  
        if (Status != OrderStatus.ReadyForDispatch)  
            throw new DomainException("Invalid state");  
  
        Status = OrderStatus.Assigned;  
    }  
  
    public void MarkDelivered()  
    {  
        if (Status != OrderStatus.InTransit)  
            throw new DomainException("Invalid state");  
  
        Status = OrderStatus.Delivered;  
    }  
}

---

## 9. Integration Points

---

### Publish

OrderCreated  
OrderReadyForDispatch  
OrderDelivered

---

### Consume

DriverAssigned

---

---

## 10. Persistence Model (MySQL)

---

### Table: Orders

CREATE TABLE Orders (  
    Id CHAR(36) PRIMARY KEY,  
    PickupLat DOUBLE,  
    PickupLng DOUBLE,  
    DeliveryLat DOUBLE,  
    DeliveryLng DOUBLE,  
    Status VARCHAR(50),  
    CreatedAt DATETIME,  
    UpdatedAt DATETIME  
);

---

Note:

Không lưu driverId ở đây nếu theo DDD strict  
(hoặc chỉ lưu reference read-only)

---

## 11. Anti-patterns

---

Order chứa driverId (coupling Fleet)  
Order gọi trực tiếp Dispatch  
Order gọi Optimizer  
Order chứa route

---

## 12. Design Guarantees

---

Order Context đảm bảo:

- State machine luôn hợp lệ
- Không bị coupling với routing/dispatch
- Có thể scale độc lập