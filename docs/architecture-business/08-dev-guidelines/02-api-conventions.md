## 1. Mục tiêu

API Conventions đảm bảo:

- Consistency giữa các service
- Dễ sử dụng cho client (web, mobile)
- Dễ version & backward-compatible
- Dễ observability & debugging

---

## 2. Nguyên tắc cốt lõi

---

### 2.1 Resource-oriented (RESTful)

/orders  
/drivers  
/vehicles

---

---

### 2.2 Noun > Verb

Dùng:              POST /orders  
Không dùng:   POST /create-order

---

---

### 2.3 Idempotency-aware

PUT /orders/{id} → idempotent  
POST /orders → non-idempotent

---

---

## 3. URL Design

---

### 3.1 Base path

/api/v1/{resource}

---

---

### 3.2 Examples

GET    /api/v1/orders  
GET    /api/v1/orders/{id}  
POST   /api/v1/orders  
PUT    /api/v1/orders/{id}  
DELETE /api/v1/orders/{id}

---

---

### 3.3 Nested resource

GET /api/v1/orders/{id}/tracking

---

---

### 3.4 Query params

GET /orders?status=ASSIGNED&page=1&pageSize=20

---

---

## 4. Request / Response Format

---

### 4.1 Standard Response

{  
  "success": true,  
  "data": {},  
  "error": null,  
  "meta": {}  
}

---

---

### 4.2 Error Response

{  
  "success": false,  
  "error": {  
    "code": "ORDER_NOT_FOUND",  
    "message": "Order not found"  
  }  
}

---

---

### 4.3 Meta (pagination)

{  
  "meta": {  
    "page": 1,  
    "pageSize": 20,  
    "total": 100  
  }  
}

---

---

## 5. Authentication & Headers

---

### 5.1 Authorization

Authorization: Bearer <JWT>

---

---

### 5.2 Correlation

X-Correlation-Id: uuid

---

---

### 5.3 Idempotency

Idempotency-Key: uuid

---

---

## 6. HTTP Status Codes

---

### Success

|Code|Meaning|
|---|---|
|200|OK|
|201|Created|
|204|No Content|

---

---

### Client Error

|Code|Meaning|
|---|---|
|400|Bad Request|
|401|Unauthorized|
|403|Forbidden|
|404|Not Found|
|409|Conflict|

---

---

### Server Error

|Code|Meaning|
|---|---|
|500|Internal Error|
|503|Service Unavailable|

---

---

## 7. Validation Rules

---

### Request validation

- Required fields  
- Format (email, phone)  
- Range (weight, distance)

---

---

### Response

400 + error details

---

---

## 8. Idempotency

---

### Required for

- POST /orders  
- Payment APIs

---

---

### Behavior

Same Idempotency-Key → same result

---

---

## 9. Async APIs

---

### Pattern

POST /dispatch  
→ returns 202 Accepted

---

---

### Response

{  
  "success": true,  
  "data": {  
    "jobId": "uuid"  
  }  
}

---

---

### Polling

GET /jobs/{id}

---

---

## 10. Pagination

---

### Query

?page=1&pageSize=20

---

---

### Response

{  
  "meta": {  
    "page": 1,  
    "pageSize": 20,  
    "total": 100  
  }  
}

---

---

## 11. Filtering & Sorting

---

### Filtering

/orders?status=DELIVERED

---

---

### Sorting

/orders?sort=createdAt_desc

---

---

## 12. Rate Limiting

---

### Headers

X-RateLimit-Limit: 100  
X-RateLimit-Remaining: 80

---

---

### Strategy

- Per user  
- Per IP

---

---

## 13. Versioning

---

### URL versioning

/api/v1/orders

---

---

### Rule

- Không break v1  
- Breaking → v2

---

---

## 14. WebSocket / Realtime

---

### Tech

- SignalR

---

---

### Endpoint

/ws/tracking

---

---

### Message format

{  
  "type": "location_update",  
  "data": {}  
}

---

---

## 15. Documentation

---

### Tool

- Swagger / OpenAPI

---

---

### Rule

- API phải có docs  
- Example request/response

---

---

## 16. Testing

---

### API tests

- Contract test  
- Integration test

---

---

### Tools

- Postman  
- Newman

---

---

## 17. Anti-patterns

---

Inconsistent response format  
Dùng verb trong URL  
Không version API  
Trả về HTTP 200 cho lỗi  
Không validate input  
Không idempotency

---

---

## 18. Design Guarantees

---

API Conventions đảm bảo:

- Client dễ tích hợp
- API consistent
- Dễ maintain & evolve
- Giảm breaking change