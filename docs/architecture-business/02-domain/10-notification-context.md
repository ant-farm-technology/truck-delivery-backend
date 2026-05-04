## 1. Mục tiêu

Notification Context chịu trách nhiệm:

- Gửi thông báo tới user (Driver, Customer, Admin)
- Hỗ trợ nhiều channel: Push, SMS, Email
- Đảm bảo **không spam + không gửi trùng**

---

## 2. Boundary & Responsibility

---

### Thuộc Notification

- Format message  
- Channel selection  
- Delivery (push, SMS, email)  
- Retry khi fail

---

### KHÔNG thuộc Notification

- Không chứa business logic  
- Không quyết định khi nào gửi (chỉ react event)  
- Không quản lý order/dispatch

Notification = **reaction layer**

---

## 3. Ubiquitous Language

Notification  
Channel  
Template  
Recipient  
DeliveryStatus  
RetryPolicy

---

## 4. Domain Model

---

### 4.1 Notification

Notification  
 ├── NotificationId  
 ├── RecipientId  
 ├── Channel (Push, SMS, Email)  
 ├── TemplateId  
 ├── Payload  
 ├── Status  
 ├── RetryCount  
 └── CreatedAt

---

### 4.2 Template

Template  
 ├── TemplateId  
 ├── Channel  
 ├── Content (with placeholders)

---

---

## 5. Event-driven Trigger

---

### Consume events

DriverAssigned  
OrderPickedUp  
OrderDelivered  
PaymentCompleted

---

### Flow

Event  
  ↓  
Notification Service  
  ↓  
Create Notification  
  ↓  
Send via channel

---

---

## 6. Channel Strategy

---

### 6.1 Push Notification

- Mobile app
- Realtime

---

### 6.2 SMS

- Critical events

---

### 6.3 Email

- Non-urgent / summary

---

---

### Rule chọn channel

- Critical → SMS + Push  
- Realtime → Push  
- Low priority → Email

---

---

## 7. Template Engine

---

### Ví dụ template

"Your order {orderId} has been assigned to driver {driverName}"

---

### Payload

{  
  "orderId": "123",  
  "driverName": "John"  
}

---

### Rule

Template phải tách khỏi code

---

---

## 8. Retry & Delivery

---

### Retry Policy

- Exponential backoff  
- Max 3–5 lần

---

### Status

PENDING → SENT → FAILED → RETRYING

---

---

## 9. Idempotency

---

### Vấn đề

Event duplicate → gửi trùng

---

### Giải pháp

IdempotencyKey = eventId + recipient + template

---

### Rule

Nếu đã gửi → skip

---

---

## 10. Rate Limiting (RẤT QUAN TRỌNG)

---

### Per user

Max 5 notifications / phút

---

### Global

Throttle theo channel

---

---

## 11. Delivery Providers

---

### Push

- Firebase / APNs

---

### SMS

- Twilio / local provider

---

### Email

- SMTP / SendGrid

---

Tất cả phải qua abstraction layer:

INotificationProvider

---

---

## 12. Processing Pipeline

---

Kafka Event  
   ↓  
Notification Consumer  
   ↓  
Create Notification  
   ↓  
Resolve Template  
   ↓  
Send (provider)  
   ↓  
Update status

---

---

## 13. Persistence Model

---

### Tables (MySQL)

Notifications  
Templates  
NotificationLogs

---

---

## 14. Observability

---

### Metrics

- Sent count  
- Failed count  
- Retry count

---

### Logging

- provider response  
- latency

---

---

## 15. Failure Handling

---

### Case 1: Provider fail

→ retry

---

### Case 2: Permanent fail

→ mark FAILED  
→ alert

---

### Case 3: Spike traffic

→ queue + throttle

---

---

## 16. Anti-patterns

---

Gửi sync trong request flow  
Không retry  
Không rate limit  
Hardcode message  
Không idempotent

---

---

## 17. Design Guarantees

---

Notification Context đảm bảo:

- Không spam user
- Không gửi trùng
- Có retry khi fail
- Scale độc lập