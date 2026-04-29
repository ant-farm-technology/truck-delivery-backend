## 1. Mục tiêu

API Contracts định nghĩa:

- Request / Response chuẩn hoá
- Versioning strategy
- Validation rules
- Error handling
- Không expose domain model

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Contract-first

API = public contract  
→ không thay đổi tuỳ tiện

---

### 2.2 DTO-only

Không expose Domain Entity  
Chỉ dùng DTO

---

### 2.3 Backward-compatible

Thêm field → OK  
Xoá field → BREAKING

---

---

## 3. API Gateway

---

### Tech

- YARP

---

### Responsibility

- Routing  
- JWT validation  
- Correlation ID  
- Rate limiting

---

---

## 4. Versioning Strategy

---

### URL-based

/api/v1/orders  
/api/v2/orders

---

### Rule

- v1 giữ nguyên  
- v2 khi breaking change

---

---

## 5. Request / Response Standard

---

### Request

{  
  "data": { ... },  
  "meta": { ... }  
}

---

### Response

{  
  "success": true,  
  "data": { ... },  
  "error": null,  
  "meta": {  
    "correlationId": "..."  
  }  
}

---

---

## 6. Order API

---

### Create Order

POST /api/v1/orders

---

#### Request

{  
  "pickup": { "lat": 0, "lng": 0 },  
  "delivery": { "lat": 0, "lng": 0 }  
}

---

#### Response

{  
  "success": true,  
  "data": {  
    "orderId": "uuid"  
  }  
}

---

---

### Get Order

GET /api/v1/orders/{id}

---

#### Response

{  
  "data": {  
    "id": "uuid",  
    "status": "Assigned",  
    "driverId": "uuid"  
  }  
}

---

---

## 7. Driver API

---

### Assign Vehicle

POST /api/v1/drivers/{id}/assign-vehicle

---

#### Request

{  
  "vehicleId": "uuid"  
}

---

---

### Update Location (Tracking)

POST /api/v1/tracking/location

---

#### Request

{  
  "lat": 0,  
  "lng": 0,  
  "speed": 40  
}

---

---

## 8. Dispatch API

---

### Trigger Optimization

POST /api/v1/dispatch/optimize

---

#### Response

{  
  "routes": []  
}

---

---

## 9. Payment API

---

### Create Payment

POST /api/v1/payments

---

#### Request

{  
  "orderId": "uuid"  
}

---

---

### Webhook (Gateway)

POST /api/v1/payments/webhook

---

Rule:

Webhook phải:  
- Verify signature  
- Idempotent

---

---

## 10. Auth API

---

### Register

POST /api/v1/auth/register

---

---

### Login

POST /api/v1/auth/login

---

#### Response

{  
  "accessToken": "...",  
  "refreshToken": "..."  
}

---

---

## 11. Tracking Realtime

---

### SignalR Hub

/ws/tracking

---

### Subscribe

tracking:{orderId}

---

---

## 12. Validation

---

### Rule

- Validate ở API layer  
- Không để invalid vào Application

---

### Example

lat ∈ [-90, 90]  
lng ∈ [-180, 180]

---

---

## 13. Error Handling

---

### Standard Error

{  
  "success": false,  
  "error": {  
    "code": "ORDER_NOT_FOUND",  
    "message": "Order not found"  
  }  
}

---

### HTTP Codes

|Code|Meaning|
|---|---|
|200|OK|
|400|Bad request|
|401|Unauthorized|
|404|Not found|
|500|Internal error|

---

---

## 14. Idempotency (API Level)

---

### Header

Idempotency-Key: uuid

---

### Rule

Same key → same result

---

---

## 15. Observability

---

### Headers

X-Correlation-Id

---

### Logging

- Request  
- Response  
- Latency

---

---

## 16. Pagination

---

### Query

GET /orders?page=1&pageSize=20

---

### Response

{  
  "data": [],  
  "meta": {  
    "page": 1,  
    "total": 100  
  }  
}

---

---

## 17. Anti-patterns

---

Expose domain entity  
Không version API  
Breaking change không báo  
Không validate input  
Không có error contract

---

---

## 18. Design Guarantees

---

API Contracts đảm bảo:

- Stable interface
- Frontend độc lập backend
- Dễ versioning
- Dễ mở rộng