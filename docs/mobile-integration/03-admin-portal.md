# Admin Portal — Integration Guide

> Truck Delivery Backend · Tài liệu tích hợp cho Admin Portal (web)
> Cập nhật: 2026-05-09 (audit từ source code thực tế)

---

## 1. Kiến trúc tổng quan

```
┌──────────────────────────────────────────────────────────────┐
│                      Admin Portal (Web)                      │
│  [Dashboard KPI] [Driver Mgmt] [Shipment] [Payment] [Fraud]  │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS + JWT Bearer (role=Admin)
                         ▼
             ┌───────────────────────┐
             │  API Gateway — YARP   │  :8080
             └───────────┬───────────┘
  ┌──────────────────────┼──────────────────────────┐
  ▼                      ▼                          ▼
Identity :8081     Driver :8083              Shipment :8086
/admin/accounts    /drivers/*               /shipments/*
/auth/me           /vehicles/*
                   /drivers/pending-verification
Payment :8089      Analytics :8092
/payments          /analytics/kpis
                   /analytics/breakdown/incidents
                   /analytics/fraud/alerts
```

---

## 2. Authentication (Admin)

Admin không tự đăng ký — chỉ Admin khác mới tạo được tài khoản Admin.

### 2.1 Tạo tài khoản Admin mới

```http
POST /api/v1/admin/accounts
Authorization: Bearer <admin-token>   (role=Admin)

{
  "email": "admin2@company.com",
  "password": "Admin@Secure123",
  "firstName": "Admin",
  "lastName": "Hai",
  "phoneNumber": "0900000002"
}
```

```json
{ "userId": "550e8400-..." }
```

### 2.2 Đăng nhập (cùng endpoint với Customer/Driver)

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "admin@company.com", "password": "Admin@Secure123" }
```

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "abc123...",
  "expiresAt": "...",
  "role": "Admin",
  "userId": "..."
}
```

### 2.3 Xem profile Admin

```http
GET /api/v1/auth/me
Authorization: Bearer <admin-token>
```

```json
{
  "id": "...",
  "email": "admin@company.com",
  "role": "Admin",
  "firstName": "Admin",
  "isActive": true,
  "createdAt": "...",
  "lastLoginAt": "..."
}
```

---

## 3. Driver Management

### 3.1 Danh sách tài xế

```http
GET /api/v1/drivers?status=2&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

`status` (int, optional): `1=Offline`, `2=Available`, `3=Busy`, `4=Suspended`

### 3.2 Chi tiết tài xế

```http
GET /api/v1/drivers/{driverId}
Authorization: Bearer <admin-token>
```

### 3.3 Danh sách chờ xác minh giấy tờ

```http
GET /api/v1/drivers/pending-verification?page=1&pageSize=20
Authorization: Bearer <admin-token>
```

```json
[
  {
    "id": "7b2f4c8e-...",
    "fullName": "Trần Văn B",
    "email": "driver@example.com",
    "phoneNumber": "0901234567",
    "verificationStatus": "PendingOcrVerification",
    "submittedAt": "2026-04-30T08:00:00Z"
  }
]
```

Bao gồm cả `PendingOcrVerification` và `ManualReview`.

### 3.4 Xác minh driver (Admin approve)

```http
POST /api/v1/drivers/{driverId}/verify
Authorization: Bearer <admin-token>

{ "notes": "Đã kiểm tra giấy tờ, hợp lệ" }
```

Response: `204 No Content`

### 3.5 Từ chối driver

```http
POST /api/v1/drivers/{driverId}/reject-verification
Authorization: Bearer <admin-token>

{ "reason": "Bằng lái hết hạn, ảnh không rõ" }
```

Response: `204 No Content`

### 3.6 Đổi trạng thái driver (Admin)

```http
PUT /api/v1/drivers/{driverId}/status
Authorization: Bearer <admin-token>

{ "status": "Suspended" }
```

Admin có thể set mọi status kể cả `Suspended (4)`.

### 3.7 Gán xe cho driver thủ công

```http
POST /api/v1/drivers/{driverId}/assign-vehicle
Authorization: Bearer <admin-token>

{ "vehicleId": "a1b2c3d4-..." }
```

Response: `204 No Content`

---

## 4. Vehicle Management

### 4.1 Danh sách xe

```http
GET /api/v1/vehicles?status=1&driverId=X&type=3&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

`status` (int): `1=Available`, `2=InUse`, `3=Breakdown`, `4=Maintenance`
`type` (int): `1=Motorbike`, `2=Van`, `3=Truck3T`, `4=Truck5T`, `5=Truck10T`, `6=Truck15T`

### 4.2 Chi tiết xe

```http
GET /api/v1/vehicles/{vehicleId}
Authorization: Bearer <admin-token>
```

### 4.3 Đăng ký xe thủ công (Admin)

```http
POST /api/v1/vehicles
Authorization: Bearer <admin-token>

{
  "licensePlate": "51A-99999",
  "brand": "Hino",
  "model": "XZU720L",
  "type": "Truck3T",
  "maxWeightKg": 3000,
  "maxVolumeCbm": 15.0,
  "lengthM": 4.2,
  "widthM": 1.8,
  "heightM": 1.8,
  "yearOfManufacture": 2021,
  "registrationNumber": "HCM-21-5678",
  "registrationExpiryDate": "2027-12-31"
}
```

### 4.4 Cập nhật trạng thái xe

```http
PUT /api/v1/vehicles/{vehicleId}/status
Authorization: Bearer <admin-token>

{ "status": "Maintenance" }
```

---

## 5. Shipment Management

### 5.1 Danh sách shipment

```http
GET /api/v1/shipments?status=InProgress&driverId=X&customerId=Y&orderId=Z&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

`status` (string): `Created`, `RoutePlanning`, `DriverAssigning`, `DriverConfirmed`,
`InProgress`, `Completed`, `Failed`, `DispatcherReviewRequired`, `Reassigning`

### 5.2 Chi tiết shipment

```http
GET /api/v1/shipments/{shipmentId}
Authorization: Bearer <admin-token>
```

### 5.3 Confirm dispatch (Admin duyệt giao đơn)

```http
POST /api/v1/shipments/{shipmentId}/confirm-dispatch
Authorization: Bearer <admin-token>
```

Response: `204 No Content`

### 5.4 Decline dispatch (Admin từ chối)

```http
POST /api/v1/shipments/{shipmentId}/decline-dispatch
Authorization: Bearer <admin-token>

{ "reason": "Không tìm được tài xế phù hợp" }
```

Response: `204 No Content`

### 5.5 Cập nhật trạng thái shipment

```http
PUT /api/v1/shipments/{shipmentId}/status
Authorization: Bearer <admin-token>

{ "status": "Failed" }
```

Admin có thể set mọi ShipmentStatus.

---

## 6. Payment Management

### 6.1 Danh sách thanh toán

```http
GET /api/v1/payments?status=Completed&dateFrom=2026-05-01&dateTo=2026-05-09&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

**PaymentStatus:** `Created`, `Pending`, `Authorized`, `Captured`, `Completed`, `Failed`, `Refunded`

### 6.2 Chi tiết thanh toán theo đơn

```http
GET /api/v1/payments/orders/{orderId}
Authorization: Bearer <admin-token>
```

### 6.3 Tạo payment thủ công (Admin)

```http
POST /api/v1/payments
Authorization: Bearer <admin-token>

{
  "orderId": "a1b2c3d4-...",
  "customerId": "550e8400-...",
  "amount": 3961000,
  "currency": "VND"
}
```

### 6.4 Xem Escrow (khi driver bị swap)

```http
GET /api/v1/payments/orders/{orderId}/escrow
Authorization: Bearer <admin-token>
```

```json
{
  "id": "escrow-uuid",
  "shipmentId": "...",
  "orderId": "...",
  "originalDriverId": "...",
  "replacementDriverId": "...",
  "lockedAmount": 3961000,
  "currency": "VND",
  "status": "Locked",
  "resolutionNote": null,
  "lockedAt": "...",
  "resolvedAt": null
}
```

**EscrowStatus:** `Locked`, `Confirmed`, `Disputed`, `Resolved`

### 6.5 Giải quyết Escrow (Admin)

```http
POST /api/v1/payments/escrow/{escrowId}/confirm
Authorization: Bearer <admin-token>

{ "note": "Admin xác nhận giao thành công" }
```

```http
POST /api/v1/payments/escrow/{escrowId}/dispute
Authorization: Bearer <admin-token>

{ "note": "Khiếu nại hợp lệ, tiến hành hoàn tiền" }
```

### 6.6 Thu nhập tài xế

```http
GET /api/v1/payments/drivers/{driverId}/earnings?dateFrom=2026-05-01&dateTo=2026-05-09&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

---

## 7. Analytics & Monitoring

### 7.1 KPI Tổng quan

```http
GET /api/v1/analytics/kpis?days=30
Authorization: Bearer <admin-token>
```

```json
{
  "periodDays": 30,
  "breakdownCount": 15,
  "successfulReassignmentCount": 12,
  "reassignmentSuccessRatePct": 80.0,
  "avgRecoveryTimeMinutes": 23.5,
  "fraudAlertCount": 3,
  "breakdownsByRiskLevel": {
    "Low": 8,
    "Medium": 5,
    "High": 2
  }
}
```

### 7.2 Sự cố hỏng xe

```http
GET /api/v1/analytics/breakdown/incidents?days=30&limit=50
Authorization: Bearer <admin-token>
```

### 7.3 Cảnh báo gian lận

```http
GET /api/v1/analytics/fraud/alerts?days=30&limit=50
Authorization: Bearer <admin-token>
```

```json
[
  {
    "id": "alert-uuid",
    "originalDriverId": "...",
    "replacementDriverId": "...",
    "swapCount": 5,
    "detectedAt": "2026-05-09T10:00:00Z",
    "isAcknowledged": false
  }
]
```

Fraud alert được tạo khi cùng cặp driver swap quá nhiều lần.

### 7.4 Xác nhận đã xử lý fraud alert

```http
POST /api/v1/analytics/fraud/alerts/{alertId}/acknowledge
Authorization: Bearer <admin-token>
```

Response: `204 No Content`

---

## 8. Luồng xử lý điển hình

### 8.1 Duyệt driver mới

```
1. GET /drivers/pending-verification → danh sách chờ
2. GET /drivers/{id}                 → xem chi tiết + ảnh giấy tờ
3. POST /drivers/{id}/verify         → approve
   hoặc POST /drivers/{id}/reject-verification → từ chối kèm lý do
```

### 8.2 Xử lý shipment bị stuck (DispatcherReviewRequired)

```
1. GET /shipments?status=DispatcherReviewRequired → danh sách cần xem
2. GET /shipments/{id}                            → xem chi tiết
3. POST /shipments/{id}/confirm-dispatch          → duyệt giao
   hoặc POST /shipments/{id}/decline-dispatch     → từ chối + lý do
```

### 8.3 Xử lý fraud alert

```
1. GET /analytics/fraud/alerts         → danh sách cảnh báo
2. Điều tra: GET /drivers/{originalId}, GET /drivers/{replacementId}
3. Hành động: suspend driver nếu cần (PUT /drivers/{id}/status Suspended)
4. POST /analytics/fraud/alerts/{id}/acknowledge
```

---

## 9. Error Codes

| Code | HTTP | Ý nghĩa |
|---|---|---|
| `Driver.NotFound` | 404 | Driver không tồn tại |
| `Driver.Verification` | 400 | Driver không đủ điều kiện verify |
| `Vehicle.NotFound` | 404 | Vehicle không tồn tại |
| `Shipment.NotFound` | 404 | Shipment không tồn tại |
| `Shipment.InvalidStatus` | 400 | Không thể chuyển sang status này |
| `Payment.Conflict` | 409 | Payment đã tồn tại |
| `Escrow.NotFound` | 404 | Escrow không tồn tại |
| `FraudAlert.NotFound` | 404 | Alert không tồn tại |
| `Forbidden` | 403 | Không có quyền Admin |
