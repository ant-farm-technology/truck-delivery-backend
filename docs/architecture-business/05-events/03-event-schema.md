## 1. 🎯 Mục tiêu

Tài liệu này định nghĩa:

- Format chuẩn cho tất cả events
- Quy tắc versioning & backward compatibility
- Schema cho các domain events chính
- Cách validate và evolve schema

---

## 2. 🧠 Nguyên tắc cốt lõi

---

### 2.1 Event là immutable

Event đã publish → không sửa

---

---

### 2.2 Backward compatible

Consumer cũ vẫn đọc được event mới

---

---

### 2.3 Self-contained

Event phải đủ dữ liệu, không phụ thuộc query thêm

---

---

### 2.4 Schema-first

Define schema trước khi code

---

---

## 3. 🧾 Event Envelope (Chuẩn bắt buộc)

---

{  
  "eventId": "uuid",  
  "eventType": "OrderCreated",  
  "eventVersion": 1,  
  "timestamp": "2026-01-01T00:00:00Z",  
  "correlationId": "uuid",  
  "causationId": "uuid",  
  "source": "order-service",  
  "data": {},  
  "metadata": {}  
}

---

---

## 4. 🔑 Field giải thích

---

|Field|Meaning|
|---|---|
|eventId|unique id (idempotency)|
|eventType|loại event|
|eventVersion|version schema|
|timestamp|thời điểm tạo|
|correlationId|trace toàn flow|
|causationId|event gây ra event này|
|source|service phát event|
|data|payload chính|
|metadata|optional|

---

---

## 5. 📦 Metadata Convention

---

{  
  "tenantId": "optional",  
  "userId": "optional",  
  "traceId": "otel-trace-id"  
}

---

---

## 6. 🧱 Domain Event Schemas

---

# 6.1 OrderCreated

---

{  
  "eventType": "OrderCreated",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "customerId": "uuid",  
    "pickupLocation": {  
      "lat": 21.0285,  
      "lng": 105.8542  
    },  
    "deliveryLocation": {  
      "lat": 21.03,  
      "lng": 105.85  
    },  
    "weight": 120.5,  
    "createdAt": "timestamp"  
  }  
}

---

---

# 6.2 RouteCalculated

---

{  
  "eventType": "RouteCalculated",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "distanceKm": 12.5,  
    "estimatedDurationSec": 1800,  
    "polyline": "encoded_string"  
  }  
}

---

---

# 6.3 DriverAssigned

---

{  
  "eventType": "DriverAssigned",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "driverId": "uuid",  
    "vehicleId": "uuid",  
    "assignedAt": "timestamp"  
  }  
}

---

---

# 6.4 OrderAssigned

---

{  
  "eventType": "OrderAssigned",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "driverId": "uuid"  
  }  
}

---

---

# 6.5 OrderPickedUp

---

{  
  "eventType": "OrderPickedUp",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "pickedUpAt": "timestamp"  
  }  
}

---

---

# 6.6 LocationUpdated

---

{  
  "eventType": "LocationUpdated",  
  "eventVersion": 1,  
  "data": {  
    "driverId": "uuid",  
    "orderId": "uuid",  
    "lat": 21.02,  
    "lng": 105.85,  
    "timestamp": "timestamp"  
  }  
}

---

---

# 6.7 OrderDelivered

---

{  
  "eventType": "OrderDelivered",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "deliveredAt": "timestamp"  
  }  
}

---

---

# 6.8 PaymentCompleted

---

{  
  "eventType": "PaymentCompleted",  
  "eventVersion": 1,  
  "data": {  
    "orderId": "uuid",  
    "amount": 500000,  
    "currency": "VND",  
    "paidAt": "timestamp"  
  }  
}

---

---

## 7. 🔄 Versioning Strategy

---

### Rule

Không sửa schema cũ → tạo version mới

---

---

### Example

---

#### v1

{  
  "weight": 100  
}

---

---

#### v2

{  
  "weight": 100,  
  "volume": 2.5  
}

---

---

### Compatibility

✔ Add field → OK  
❌ Remove field → break  
❌ Rename field → break

---

---

## 8. 📐 Schema Format

---

### Option 1

JSON Schema

---

---

### Option 2

Avro (recommended for Kafka)

---

---

## 9. 🔐 Validation

---

### Producer

Validate trước khi publish

---

---

### Consumer

Validate trước khi process

---

---

## 10. 📦 Schema Registry (Recommended)

---

### Purpose

Quản lý version & compatibility

---

---

### Tools

- Confluent Schema Registry

---

---

## 11. ⚠️ Evolution Strategy

---

### Add field

Optional → safe

---

---

### Remove field

Deprecated → remove sau

---

---

### Rename

Add field mới → deprecate field cũ

---

---

## 12. 📊 Event Size Rule

---

< 1MB (Kafka best practice)

---

---

## 13. ⚠️ Anti-patterns

---

❌ Event thiếu context  
❌ Event quá to  
❌ Không version  
❌ Không correlationId  
❌ Reuse event cho nhiều purpose

---

---

## 14. 🎯 Design Guarantees

---

Event schema đảm bảo:

- Consistent giữa services
- Dễ evolve
- Dễ debug
- Tương thích lâu dài

---

---

# ✅ Kết luận

Event schema là:

> **“Ngôn ngữ chung của hệ thống event-driven”**

---

## Insight quan trọng nhất

> **Một khi event đã publish — nó trở thành contract vĩnh viễn**