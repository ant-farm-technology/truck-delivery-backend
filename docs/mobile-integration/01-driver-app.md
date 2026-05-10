# Driver App — Mobile Integration Guide

> Truck Delivery Backend · Tài liệu tích hợp cho ứng dụng mobile tài xế
> Cập nhật: 2026-05-10 (đồng bộ sau BE-FIX-1…5)

---

## 1. Kiến trúc tổng quan

```
┌──────────────────────────────────────────────────────────────┐
│                     Driver App (Mobile)                      │
│  [Onboarding] [Dashboard] [Delivery] [Breakdown] [Profile]   │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS + JWT Bearer + X-Correlation-Id
                         ▼
             ┌───────────────────────┐
             │  API Gateway — YARP   │  :8080
             └───────────┬───────────┘
        ┌────────────────┼──────────────────────┐
        ▼                ▼                      ▼
  Identity :8081   Driver :8083          Tracking :8087
  Auth/Me          /drivers/*            /tracking/*
                   /vehicles/*           SignalR /hubs/tracking
                   /uploads/*

  Shipment :8086              Notification :8088
  /shipments/*                /notifications/register-device
```

**Services nội bộ (không expose qua Gateway):**
- Route `:8084` (Rust), Optimizer `:8085` (Python), OCR `:8090`

---

## 2. Luồng tổng quan

```
ONBOARDING (1 lần)
  Bước 1 → POST /api/v1/auth/register/driver       (Identity)
  Bước 2 → GET  /api/v1/uploads/presigned-url       (Driver)
  Bước 2 → PUT <presigned-url>                      (MinIO direct)
  Bước 3 → POST /api/v1/drivers/register            (Driver — all-in-one)
          ↓ Backend publish → OCR async verify
  Poll hoặc push notification → verificationStatus thay đổi

MAIN FLOW (hằng ngày)
  GET  /api/v1/drivers/me                           lấy profile + driverId
  PUT  /api/v1/drivers/{id}/status  Available       bắt đầu nhận đơn
  ← SignalR DriverAssigned event
  GET  /api/v1/shipments/{id}                       lấy chi tiết đơn
  POST /api/v1/tracking/location  (mỗi 1–5s)       push GPS
  PUT  /api/v1/shipments/{id}/status  InProgress    đã lấy hàng + đang giao
  PUT  /api/v1/shipments/{id}/status  Completed     giao xong
  PUT  /api/v1/drivers/{id}/status  Offline/Available
```

---

## 3. Authentication

### 3.1 Đăng ký tài khoản driver (Bước 1 Onboarding)

```http
POST /api/v1/auth/register/driver
Content-Type: application/json

{
  "email": "driver@example.com",
  "password": "P@ssw0rd123",
  "firstName": "Văn B",
  "lastName": "Trần",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1990-05-15"
}
```

> **Không dùng** `POST /api/v1/auth/register` (endpoint đó tạo Customer). Driver dùng `/register/driver`.

```json
{ "userId": "550e8400-e29b-41d4-a716-446655440000" }
```

### 3.2 Đăng nhập

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "driver@example.com", "password": "P@ssw0rd123" }
```

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "abc123...",
  "expiresAt": "2026-06-01T10:30:00Z",
  "role": "Driver",
  "userId": "550e8400-..."
}
```

### 3.3 Refresh token

```http
POST /api/v1/auth/refresh

{ "userId": "550e8400-...", "refreshToken": "abc123..." }
```

Rotation enforced: old token bị invalidate ngay. TTL 30 ngày.

### 3.4 Logout

```http
POST /api/v1/auth/logout
Authorization: Bearer <token>
```

Response: `204 No Content`

### 3.5 Headers bắt buộc cho mọi request

```http
Authorization: Bearer <accessToken>
X-Correlation-Id: <uuid-v4>
Content-Type: application/json
```

### 3.6 Lấy profile tài khoản hiện tại

```http
GET /api/v1/auth/me
Authorization: Bearer <token>
```

```json
{
  "id": "550e8400-...",
  "email": "driver@example.com",
  "firstName": "Văn B",
  "lastName": "Trần",
  "role": "Driver",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1990-05-15",
  "isActive": true,
  "createdAt": "2026-04-30T08:00:00Z",
  "lastLoginAt": "2026-05-09T05:00:00Z"
}
```

---

## 4. Onboarding — Bước 2: Upload giấy tờ lên MinIO

### 4.1 Xin pre-signed URL (7 ảnh giấy tờ)

```http
GET /api/v1/uploads/presigned-url?type=driver-document
Authorization: Bearer <token>   (role=Driver)
```

```json
{
  "urls": [
    { "field": "portrait",          "uploadUrl": "http://minio:9000/driver-documents/uuid-portrait.jpg?X-Amz-Signature=...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/portrait-{uuid}.jpg" },
    { "field": "id-card-front",     "uploadUrl": "...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/id-card-front-{uuid}.jpg" },
    { "field": "id-card-back",      "uploadUrl": "...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/id-card-back-{uuid}.jpg" },
    { "field": "license-front",     "uploadUrl": "...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/license-front-{uuid}.jpg" },
    { "field": "license-back",      "uploadUrl": "...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/license-back-{uuid}.jpg" },
    { "field": "vehicle-reg-front", "uploadUrl": "...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/vehicle-reg-front-{uuid}.jpg" },
    { "field": "vehicle-reg-back",  "uploadUrl": "...", "finalUrl": "http://minio:9000/driver-documents/{driverId}/vehicle-reg-back-{uuid}.jpg" }
  ]
}
```

### 4.2 Upload từng ảnh lên MinIO

```http
PUT <entry.uploadUrl>
Content-Type: image/jpeg

<binary image data>
```

Response: `200 OK`. URL TTL: 15 phút.

---

## 5. Onboarding — Bước 3: Đăng ký thông tin tài xế + xe (all-in-one)

```http
POST /api/v1/drivers/register
Authorization: Bearer <token>   (role=Driver)
Content-Type: application/json

{
  "idCardNumber": "079123456789",
  "dateOfBirth": "1990-05-15",
  "address": "123 Nguyễn Huệ, TP.HCM",
  "licenseNumber": "123456789012",
  "licenseGrade": "C",
  "licenseExpiryDate": "2028-12-31",
  "photos": {
    "portraitUrl": "http://minio:9000/driver-documents/{driverId}/portrait-{uuid}.jpg",
    "idCardFrontUrl": "http://minio:9000/driver-documents/{driverId}/id-card-front-{uuid}.jpg",
    "idCardBackUrl": "http://minio:9000/driver-documents/{driverId}/id-card-back-{uuid}.jpg",
    "licenseFrontUrl": "http://minio:9000/driver-documents/{driverId}/license-front-{uuid}.jpg",
    "licenseBackUrl": "http://minio:9000/driver-documents/{driverId}/license-back-{uuid}.jpg",
    "vehicleRegFrontUrl": "http://minio:9000/driver-documents/{driverId}/vehicle-reg-front-{uuid}.jpg",
    "vehicleRegBackUrl": "http://minio:9000/driver-documents/{driverId}/vehicle-reg-back-{uuid}.jpg"
  },
  "vehicle": {
    "licensePlate": "51A-12345",
    "brand": "Hino",
    "model": "XZU720L",
    "type": "Truck3T",
    "maxWeightKg": 3000,
    "maxVolumeCbm": 15.0,
    "lengthM": 4.2,
    "widthM": 1.8,
    "heightM": 1.8,
    "yearOfManufacture": 2020,
    "registrationNumber": "HCM-20-1234",
    "registrationExpiryDate": "2026-12-31"
  }
}
```

> `userId`, `email`, `firstName`, `lastName`, `phoneNumber` lấy tự động từ JWT — không cần truyền.
> `finalUrl` từ §4.1 làm giá trị các trường trong `photos`.

```json
{
  "driverId": "7b2f4c8e-...",
  "vehicleId": "a1b2c3d4-...",
  "verificationStatus": "PendingOcrVerification"
}
```

**Lưu `driverId`** — dùng cho tất cả API sau.

**Error codes:**
| HTTP | Code | Ý nghĩa |
|---|---|---|
| 409 | `Driver.Conflict` | userId đã có driver profile |
| 409 | `Driver.IdCard.Conflict` | IdCardNumber đã tồn tại |
| 409 | `Vehicle.LicensePlate.Conflict` | Biển số đã tồn tại |
| 400 | `Driver.LicenseExpiryDate` | Bằng lái đã hết hạn |
| 400 | `Driver.LicenseGrade` | Hạng bằng không hợp lệ (B1, E không được dùng) |

**`licenseGrade` hợp lệ để chạy xe tải:** `B2`, `C`, `D`, `FC`, `FD`

**`type` (VehicleType):** `Motorbike`, `Van`, `Truck3T`, `Truck5T`, `Truck10T`, `Truck15T`

---

## 6. Onboarding — Theo dõi trạng thái xác minh

```http
GET /api/v1/drivers/me
Authorization: Bearer <token>   (role=Driver)
```

```json
{
  "id": "7b2f4c8e-...",
  "email": "driver@example.com",
  "firstName": "Văn B",
  "lastName": "Trần",
  "phoneNumber": "0901234567",
  "licenseNumber": "123456789012",
  "status": "Offline",
  "verificationStatus": "PendingOcrVerification",
  "licenseGrade": "C",
  "trustScore": 70,
  "currentVehicleId": "a1b2c3d4-...",
  "isActive": true,
  "createdAt": "2026-04-30T08:00:00Z"
}
```

> `id` trong response chính là `driverId`. Dùng endpoint này thay vì `/drivers/{id}` để không cần lưu driverId trước.

**`verificationStatus` enum:**

| Value | Ý nghĩa | Hành động |
|---|---|---|
| `Draft` | Chưa submit giấy tờ | Hoàn tất Bước 3 |
| `PendingOcrVerification` | Chờ OCR xử lý (vài phút) | Polling hoặc chờ push |
| `OcrVerified` | OCR xác minh thành công | Có thể set Available |
| `ManualReview` | Confidence thấp, Admin đang review | Chờ 1–2 ngày |
| `AdminVerified` | Admin duyệt thủ công | Có thể set Available |
| `Rejected` | Bị từ chối | Xem lý do, liên hệ support |

> **Chỉ khi `verificationStatus = OcrVerified | AdminVerified`** thì mới được set status `Available`.

---

## 7. Dashboard — Màn hình chính

### 7.1 Lấy thông tin tài xế + xe

Gọi **2 requests song song** (parallel) để tối ưu thời gian load:

```http
GET /api/v1/drivers/me
Authorization: Bearer <token>
```

```http
GET /api/v1/vehicles/mine
Authorization: Bearer <token>   (role=Driver)
```

> `GET /vehicles/mine` là endpoint mới — không cần biết `vehicleId` trước. Trả `404` khi driver chưa được gán xe.

**Response `GET /vehicles/mine`:**

```json
{
  "id": "a1b2c3d4-...",
  "licensePlate": "51A-12345",
  "brand": "Hino",
  "model": "XZU720L",
  "type": "Truck3T",
  "maxWeightKg": 3000,
  "maxVolumeCbm": 15.0,
  "lengthM": 4.2,
  "widthM": 1.8,
  "heightM": 1.8,
  "yearOfManufacture": 2020,
  "registrationNumber": "HCM-20-1234",
  "registrationExpiryDate": "2026-12-31",
  "status": "Available",
  "assignedDriverId": "7b2f4c8e-...",
  "createdAt": "2026-04-30T08:00:00Z"
}
```

### 7.2 Bắt đầu/dừng nhận đơn

```http
PUT /api/v1/drivers/{driverId}/status
Authorization: Bearer <token>   (role=Driver — chỉ update status của chính mình)

{ "status": "Available" }
```

**`status` hợp lệ cho Driver role:** `"Offline"`, `"Available"` — không set được `"Busy"` hay `"Suspended"`.

Response: `204 No Content`

**Luồng trạng thái driver:**
```
Offline ←→ Available → Busy (hệ thống set khi nhận đơn)
              ↑
        (chỉ khi đã OcrVerified | AdminVerified)
```

### 7.3 Xem shipment đang thực hiện

```http
GET /api/v1/shipments?driverId={driverId}&status=InProgress
Authorization: Bearer <token>
```

```json
{
  "items": [
    {
      "id": "c4d5e6f7-...",
      "orderId": "a1b2c3d4-...",
      "customerId": "...",
      "status": "InProgress",
      "pickupCity": "TP. Hồ Chí Minh",
      "pickupProvince": "Hồ Chí Minh",
      "deliveryCity": "Hà Nội",
      "deliveryProvince": "Hà Nội",
      "totalWeightKg": 45.0,
      "totalVolumeCbm": 0.756,
      "assignedDriverId": "7b2f4c8e-...",
      "assignedVehicleId": "a1b2c3d4-...",
      "distanceMeters": 1730000,
      "createdAt": "2026-04-30T08:00:00Z"
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

---

## 8. Nhận assignment qua SignalR

### 8.1 Kết nối

```dart
// Flutter (signalr_netcore)
final connection = HubConnectionBuilder()
  .withUrl(
    "https://api.example.com/hubs/tracking",
    options: HttpConnectionOptions(
      accessTokenFactory: () async => await getAccessToken(),
    ),
  )
  .withAutomaticReconnect()
  .build();

await connection.start();
```

### 8.2 Join driver group

```dart
await connection.invoke("JoinDriverGroup", args: [driverId]);
```

### 8.3 Event: DriverAssigned

```dart
connection.on("DriverAssigned", (args) {
  // args[0]: { "shipmentId": "...", "orderId": "...", "vehicleId": "...", "assignedAt": "..." }
  final shipmentId = args[0]['shipmentId'];
  fetchShipmentDetail(shipmentId);  // GET /api/v1/shipments/{shipmentId}
  showAssignmentNotification();
});
```

### 8.4 Xử lý reconnect

```dart
connection.onreconnected((connectionId) async {
  await connection.invoke("JoinDriverGroup", args: [driverId]);
});
```

---

## 9. Active Delivery

### 9.1 GPS Push (mỗi 1–5 giây)

```http
POST /api/v1/tracking/location
Authorization: Bearer <token>   (role=Driver)
Rate limit: 120 req/phút/user

{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "speedKmh": 45.5,
  "headingDeg": 270.0
}
```

Response: `204 No Content`. `driverId` lấy tự động từ JWT `sub`.

**Adaptive interval:**
| Trạng thái | Interval |
|---|---|
| Đang di chuyển (speed > 5km/h) | 1–2s |
| Dừng ngắn (≤ 5km/h, < 30s) | 5s |
| Dừng lâu (> 30s bất động) | 15–30s |
| Shipment Completed | Dừng hẳn |

### 9.2 Flush offline cache (batch)

```http
POST /api/v1/tracking/batch
Authorization: Bearer <token>
Rate limit: 10 req/phút/user

{
  "points": [
    {
      "latitude": 10.7769,
      "longitude": 106.7009,
      "recordedAt": "2026-05-09T10:05:00Z",
      "speedKmh": 45.5,
      "headingDeg": 270.0
    }
  ]
}
```

| Constraint | Giá trị |
|---|---|
| Max points/call | 100 |
| `recordedAt` | ≤ hiện tại, ≥ 24h trước |

Response: `204 No Content`

### 9.3 Cập nhật trạng thái shipment

```http
PUT /api/v1/shipments/{shipmentId}/status
Authorization: Bearer <token>   (role=Driver)

{ "status": "InProgress" }
```

**Driver được phép set:**

| Status | Ý nghĩa | Khi nào |
|---|---|---|
| `InProgress` | Đã lấy hàng và đang giao | Sau khi lấy hàng |
| `Completed` | Giao thành công | Khách ký nhận xong |

> **Lưu ý thực tế:** `ShipmentStatus` chỉ có `InProgress` và `Completed` từ góc độ driver. Các status như `PickedUp`, `InTransit`, `Delivered` thuộc về `OrderStatus` (phản ánh sang Order service tự động qua Kafka — driver không cần gọi).

Response: `204 No Content` | `403 Forbidden` nếu set status khác

### 9.4 Xem lịch sử GPS (debug)

```http
GET /api/v1/tracking/shipments/{shipmentId}/points?limit=100
Authorization: Bearer <token>
```

```json
[
  { "driverId": "...", "latitude": 10.7769, "longitude": 106.7009, "speedKmh": 45.5, "recordedAt": "..." }
]
```

---

## 10. Báo cáo hỏng xe (Breakdown)

### 10.1 Luồng

```
1. Driver nhấn "Báo hỏng xe"
2. Upload ảnh hỏng xe (≥1 ảnh)
3. Lấy GPS hiện tại
4. POST /api/v1/drivers/{driverId}/report-breakdown
5. Anti-Fraud Gate kiểm tra: TrustScore ≥ 30, ≥1 ảnh, GPS hợp lệ
6. Pass → Shipment → Reassigning → Backend tìm driver mới
```

### 10.2 Xin presigned URL ảnh hỏng xe

```http
GET /api/v1/uploads/presigned-url?type=breakdown-photo&count=2
Authorization: Bearer <token>
```

```json
{
  "urls": [
    { "field": "photo_1", "uploadUrl": "http://minio:9000/breakdown-photos/uuid1.jpg?...", "finalUrl": "breakdown-photos/uuid1.jpg" },
    { "field": "photo_2", "uploadUrl": "...", "finalUrl": "breakdown-photos/uuid2.jpg" }
  ]
}
```

`count`: 1–10. Upload từng ảnh qua `PUT <uploadUrl>`.

### 10.3 Báo hỏng xe

```http
POST /api/v1/drivers/{driverId}/report-breakdown
Authorization: Bearer <token>   (role=Driver)

{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "photoUrls": [
    "breakdown-photos/uuid1.jpg",
    "breakdown-photos/uuid2.jpg"
  ]
}
```

```json
{
  "reportId": "d5e6f7a8-...",
  "fraudRiskLevel": "Low",
  "accepted": true
}
```

**`fraudRiskLevel`:** `Unknown`, `Low`, `Medium`, `High`, `Confirmed`

**Trường hợp bị từ chối (422):**
- TrustScore < 30
- `photoUrls` rỗng
- GPS cách xa vị trí tracking hiện tại quá nhiều

> TrustScore bị trừ -3 mỗi lần báo hỏng (dù accepted). Mặc định 70, tối thiểu 0, tối đa 100.

---

## 11. Push Notifications (FCM)

### 11.1 Đăng ký FCM token

```http
POST /api/v1/notifications/register-device
Authorization: Bearer <token>

{ "token": "fcm-device-token...", "platform": "Android" }
```

`platform`: `"Android"` | `"Ios"`. Response: `204 No Content`.
Gọi lại khi token FCM thay đổi (mỗi lần app khởi động là thực hành tốt).

### 11.2 Notifications driver nhận được

| Trigger | Nội dung | Hành động khi tap |
|---|---|---|
| Được giao đơn | "Bạn vừa được giao đơn hàng mới" | Mở màn hình shipment detail |
| OCR xác minh xong | "Tài khoản đã được xác minh" | Mở dashboard |
| ManualReview | "Giấy tờ đang chờ Admin duyệt" | Mở màn hình verification status |
| Bị từ chối | "Giấy tờ bị từ chối, vui lòng liên hệ support" | Mở màn hình support |

---

## 12. Offline & Resilience

| Tình huống | Xử lý |
|---|---|
| Mất mạng khi giao hàng | Cache GPS points, dùng batch endpoint khi có mạng |
| Token hết hạn | Auto-refresh silent, nếu thất bại → redirect login |
| SignalR disconnect | `withAutomaticReconnect()` + re-join group sau reconnect |
| Báo hỏng bị reject (422) | Show lý do cụ thể, không retry tự động |

---

## 13. Enums tham chiếu

### DriverStatus
| Value | Int | Ý nghĩa |
|---|---|---|
| `Offline` | 1 | Không nhận đơn |
| `Available` | 2 | Sẵn sàng nhận đơn |
| `Busy` | 3 | Đang giao hàng (hệ thống set) |
| `Suspended` | 4 | Bị khóa |

### DriverVerificationStatus
| Value | Int |
|---|---|
| `Draft` | 0 |
| `PendingOcrVerification` | 1 |
| `OcrVerified` | 2 |
| `ManualReview` | 3 |
| `AdminVerified` | 4 |
| `Rejected` | 5 |

### ShipmentStatus (driver-visible)
| Value | Ý nghĩa |
|---|---|
| `InProgress` | Đang giao (driver set) |
| `Completed` | Giao xong (driver set) |
| `Reassigning` | Đang tìm driver khác (sau breakdown) |

### LicenseGrade (hợp lệ cho xe tải)
`B2`, `C`, `D`, `FC`, `FD` *(B1, E không được nhận đơn)*

### VehicleType
`Motorbike`, `Van`, `Truck3T`, `Truck5T`, `Truck10T`, `Truck15T`

---

## 14. Error Codes tham chiếu

| Code | HTTP | Xử lý UI |
|---|---|---|
| `Driver.NotFound` | 404 | Driver profile chưa tồn tại — redirect onboarding |
| `Driver.Conflict` | 409 | UserId đã có driver profile |
| `Driver.IdCard.Conflict` | 409 | CCCD đã đăng ký |
| `Vehicle.LicensePlate.Conflict` | 409 | Biển số đã đăng ký |
| `Driver.Verification` | 400 | Chưa verified, không thể set Available |
| `Driver.Forbidden` | 403 | Driver cố update status của người khác |
| `Breakdown.FraudGate` | 422 | Báo hỏng bị reject — show lý do |
| `Unauthorized` | 401 | Token hết hạn — refresh hoặc re-login |
