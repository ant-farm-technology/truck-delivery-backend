# Driver App — Mobile Integration Guide

> Truck Delivery Backend · Tài liệu tích hợp cho ứng dụng mobile tài xế
> Cập nhật: 2026-05-01 (Sprint 4)

---

## 1. Tổng quan kiến trúc

```
┌──────────────────────────────────────────────────────────────┐
│                     Driver App (Mobile)                      │
│                                                              │
│  [Onboarding] [Dashboard] [Delivery] [Breakdown] [Profile]   │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS + JWT Bearer
                         │ X-Correlation-Id header
                         ▼
             ┌───────────────────────┐
             │  API Gateway — YARP   │
             │        :8080          │
             └───────────┬───────────┘
                         │ path prefix routing
        ┌────────────────┼────────────────────┐
        ▼                ▼                    ▼
  Identity :8081   Driver :8083        Tracking :8087
  Order    :8082   Shipment :8086      (SignalR /hubs/tracking)
  Payment  :8089   OCR :8090 (internal)

[Push Notifications]  ← Notification Service :8088 → FCM
[Real-time updates]   ← SignalR WebSocket (/hubs/tracking)
```

**Internal services (không expose qua Gateway):**
- Route `:8084` (Rust) — routing
- Optimizer `:8085` (Python) — assignment logic

---

## 2. Luồng màn hình tổng quan

```
┌─────────────────────────────────────────────────────────────┐
│  ONBOARDING (3 bước — chỉ làm 1 lần)                        │
│                                                             │
│  Bước 1: Đăng ký tài khoản (Identity)                       │
│  Bước 2: Điền thông tin tài xế (Driver Service)             │
│  Bước 3: Upload & OCR giấy tờ → Xác minh (OCR Service)      │
└─────────────────────────────────────────────────────────────┘
                            ↓ (sau khi OcrVerified)
┌─────────────────────────────────────────────────────────────┐
│  MAIN FLOW (hằng ngày)                                      │
│                                                             │
│  Dashboard → Set Available → Nhận assignment (push/WS)      │
│           → Active Delivery → GPS push loop                 │
│           → Update status (PickedUp → Delivered)            │
│           → Set Available (tiếp chuyến mới)                 │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Authentication

### 3.1 Đăng ký tài khoản (Bước 1 Onboarding)

```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "driver@example.com",
  "password": "P@ssw0rd123",
  "firstName": "Văn B",
  "lastName": "Trần",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1990-05-15",
  "role": 2
}
```

`role`: Customer=1, Driver=2, Admin=3

```json
{
  "success": true,
  "data": { "userId": "550e8400-e29b-41d4-a716-446655440000" }
}
```

### 3.2 Đăng nhập

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "driver@example.com",
  "password": "P@ssw0rd123"
}
```

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGci...",
    "refreshToken": "550e8400-...",
    "expiresIn": 3600,
    "role": "Driver",
    "userId": "550e8400-..."
  }
}
```

### 3.3 Refresh token

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "userId": "550e8400-...",
  "refreshToken": "abc123..."
}
```

Rotation được enforce: mỗi lần refresh → old token bị invalidate ngay. TTL refresh token: 30 ngày.

**Khuyến nghị:** Auto-refresh khi còn 60s trước khi hết hạn. Lưu cả `accessToken` và `refreshToken` vào secure storage (Keychain / Keystore).

### 3.4 Headers bắt buộc cho mọi request

```http
Authorization: Bearer <accessToken>
X-Correlation-Id: <uuid-v4>       (app tự sinh, giữ trong session)
Content-Type: application/json
```

---

## 4. Onboarding — Bước 2: Đăng ký thông tin tài xế + xe (all-in-one)

> Sau khi upload ảnh giấy tờ lên MinIO (§5), gọi một endpoint duy nhất để tạo Driver + Vehicle và submit documents.

```http
POST /api/v1/drivers/register
Authorization: Bearer <token>     (role=Driver)
Content-Type: application/json

{
  "userId": "550e8400-...",
  "fullName": "Trần Văn B",
  "idCardNumber": "079123456789",
  "dateOfBirth": "1990-05-15",
  "address": "123 Nguyễn Huệ, TP.HCM",
  "licenseGrade": "C",
  "licenseExpiryDate": "2028-12-31",
  "frontIdCardUrl": "trucker-driver-docs/uuid-front-id.jpg",
  "backIdCardUrl": "trucker-driver-docs/uuid-back-id.jpg",
  "selfieUrl": "trucker-driver-docs/uuid-selfie.jpg",
  "frontLicenseUrl": "trucker-driver-docs/uuid-front-lic.jpg",
  "backLicenseUrl": "trucker-driver-docs/uuid-back-lic.jpg",
  "vehicleRegUrl": "trucker-driver-docs/uuid-vehicle-reg.jpg",
  "vehicleFrontUrl": "trucker-driver-docs/uuid-vehicle-front.jpg",
  "vehicleLicensePlate": "51A-12345",
  "vehicleType": "Truck3T",
  "vehicleMaxWeightKg": 3000,
  "vehicleVolumeCbm": 15.0,
  "vehicleLengthM": 4.2,
  "vehicleWidthM": 1.8,
  "vehicleHeightM": 1.8,
  "vehicleRegistrationNumber": "HCM-20-1234",
  "vehicleRegistrationExpiryDate": "2026-12-31"
}
```

```json
{
  "success": true,
  "data": {
    "driverId": "7b2f4c8e-...",
    "verificationStatus": "PendingOcrVerification"
  }
}
```

> **Lưu `driverId`** — dùng cho tất cả request sau này.

Sau khi gọi endpoint này, backend tự động publish `DriverDocumentsSubmittedEvent` → OCR service xử lý bất đồng bộ.

---

## 5. Onboarding — Bước 3: Upload giấy tờ & OCR xác minh

### 5.1 Luồng tổng quan

```
1. Driver app xin pre-signed URL từ backend (7 URLs)
2. Upload 7 ảnh trực tiếp lên MinIO qua pre-signed URL
3. (Optional) Gọi OCR extract endpoints → nhận data tự động điền form
4. Driver review, chỉnh sửa nếu cần
5. Submit toàn bộ data + photo URLs qua POST /api/v1/drivers/register (all-in-one)
6. Backend tự động publish DriverDocumentsSubmittedEvent → OCR verify async
7. App poll hoặc nhận push notification về verification status
```

### 5.2 Xin pre-signed URL upload (7 URLs cho 7 ảnh)

```http
GET /api/v1/uploads/presigned-url?type=driver-document
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "urls": [
      "http://minio:9000/trucker-driver-docs/uuid-front-id.jpg?X-Amz-Signature=...",
      "http://minio:9000/trucker-driver-docs/uuid-back-id.jpg?X-Amz-Signature=...",
      "http://minio:9000/trucker-driver-docs/uuid-selfie.jpg?X-Amz-Signature=...",
      "http://minio:9000/trucker-driver-docs/uuid-front-lic.jpg?X-Amz-Signature=...",
      "http://minio:9000/trucker-driver-docs/uuid-back-lic.jpg?X-Amz-Signature=...",
      "http://minio:9000/trucker-driver-docs/uuid-vehicle-reg.jpg?X-Amz-Signature=...",
      "http://minio:9000/trucker-driver-docs/uuid-vehicle-front.jpg?X-Amz-Signature=..."
    ]
  }
}
```

Thứ tự: `frontId`, `backId`, `selfie`, `frontLicense`, `backLicense`, `vehicleReg`, `vehicleFront`.

**Tiến hành upload từng ảnh:**

```http
PUT <urls[i]>
Content-Type: image/jpeg

<binary image data>
```

Response: `200 OK` (MinIO direct upload). URL TTL: 15 phút.

### 5.3 OCR Auto-fill — CCCD/CMND (optional, direct to OCR service)

> **Lưu ý:** Các endpoint OCR extract bên dưới gọi trực tiếp tới OCR Service `:8090` — **không đi qua Gateway**. Chỉ dùng khi muốn auto-fill form cho driver. Xác minh chính thức vẫn diễn ra async qua Kafka sau khi submit.

Sau khi upload ảnh CCCD mặt trước + mặt sau:

```http
POST http://ocr-service:8090/api/v1/ocr/extract/id-card
Content-Type: application/json

{
  "front_url": "http://minio:9000/trucker-driver-docs/uuid-front-id.jpg",
  "back_url": "http://minio:9000/trucker-driver-docs/uuid-back-id.jpg"
}
```

```json
{
  "success": true,
  "data": {
    "id_number": "079123456789",
    "full_name": "TRẦN VĂN B",
    "date_of_birth": "1990-05-15",
    "gender": "Nam",
    "nationality": "Việt Nam",
    "place_of_origin": "TP. Hồ Chí Minh",
    "place_of_residence": "123 Nguyễn Huệ, Quận 1, TP.HCM",
    "expiry_date": "2030-05-15",
    "confidence": 0.91,
    "suggested_form_values": {
      "id_number": "079123456789",
      "full_name": "Trần Văn B",
      "date_of_birth": "1990-05-15"
    }
  }
}
```

> Dùng `suggested_form_values` để pre-fill form. `full_name` đã được normalize từ ALL-CAPS.

### 5.4 OCR Auto-fill — Giấy phép lái xe

```http
POST http://ocr-service:8090/api/v1/ocr/extract/license
Content-Type: application/json

{
  "front_url": "http://minio:9000/trucker-driver-docs/uuid-front-lic.jpg",
  "back_url": "http://minio:9000/trucker-driver-docs/uuid-back-lic.jpg"
}
```

```json
{
  "success": true,
  "data": {
    "license_number": "123456789012",
    "grade": "C",
    "full_name": "Trần Văn B",
    "date_of_birth": "1990-05-15",
    "issue_date": "2015-03-20",
    "expiry_date": "2025-03-20",
    "issuing_authority": "Cục Đường bộ Việt Nam",
    "confidence": 0.88,
    "suggested_form_values": {
      "license_number": "123456789012",
      "license_grade": "C",
      "license_expiry": "2025-03-20"
    }
  }
}
```

**Hạng bằng lái hợp lệ để chạy xe tải:** `B2`, `C`, `D`, `E`, `FC`, `FD`

### 5.5 OCR Auto-fill — Đăng ký xe

> **Lưu ý:** Endpoint gọi trực tiếp tới OCR Service `:8090` — không đi qua Gateway.

```http
POST http://ocr-service:8090/api/v1/ocr/extract/vehicle-reg
Content-Type: application/json

{
  "front_url": "http://minio:9000/trucker-driver-docs/uuid-vehicle-reg.jpg",
  "back_url": null
}
```

```json
{
  "success": true,
  "data": {
    "license_plate": "51C-12345",
    "brand": "HINO",
    "model": "XZU720L",
    "year_of_manufacture": 2020,
    "chassis_number": "JHDFF1JK1NX000123",
    "engine_number": "A05C0000456",
    "registration_number": "HCM-20-1234",
    "owner_name": "TRẦN VĂN B",
    "owner_id_number": "079123456789",
    "expiry_date": "2025-12-31",
    "confidence": 0.85,
    "suggested_form_values": {
      "license_plate": "51C-12345",
      "registration_number": "HCM-20-1234"
    }
  }
}
```

### 5.6 Kiểm tra trạng thái xác minh

```http
GET /api/v1/drivers/{driverId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "7b2f4c8e-...",
    "fullName": "Trần Văn B",
    "verificationStatus": "OcrVerified",
    "status": "Offline",
    "assignedVehicle": null
  }
}
```

**`verificationStatus` enum:**

| Value | Ý nghĩa | Hành động cần làm |
|---|---|---|
| `PendingOcrVerification` | Chờ OCR xử lý (vài phút) | Polling hoặc chờ push notification |
| `OcrVerified` | OCR xác minh tự động thành công | Có thể bắt đầu nhận đơn |
| `ManualReview` | Confidence thấp, Admin đang review | Chờ Admin duyệt (có thể mất 1–2 ngày) |
| `Rejected` | Bị từ chối | Xem lý do, upload lại giấy tờ |
| `AdminVerified` | Admin đã duyệt thủ công | Có thể bắt đầu nhận đơn |

> **Chỉ khi `verificationStatus = OcrVerified | AdminVerified`** thì driver mới có thể set status `Available`.

---

## 6. Dashboard — Màn hình chính

### 6.1 Xem thông tin tài xế

```http
GET /api/v1/drivers/{driverId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "7b2f4c8e-...",
    "fullName": "Trần Văn B",
    "phone": "0901234567",
    "status": "Available",
    "verificationStatus": "OcrVerified",
    "trustScore": 70,
    "assignedVehicle": {
      "id": "a1b2c3d4-...",
      "licensePlate": "51A-12345",
      "type": "Truck3T",
      "maxWeightKg": 3000,
      "lengthM": 4.2,
      "widthM": 1.8,
      "heightM": 1.8
    }
  }
}
```

### 6.2 Đổi trạng thái

```http
PUT /api/v1/drivers/{driverId}/status
Authorization: Bearer <token>

{
  "status": "Available"
}
```

**Luồng trạng thái driver:**

```
Offline  ←→  Available  →  Busy (hệ thống tự set khi nhận đơn)
               ↑
         (chỉ khi đã Verified)
```

| Action | Status gửi lên | Khi nào |
|---|---|---|
| Bắt đầu nhận đơn | `Available` | Sau khi verified, sẵn sàng chạy |
| Nghỉ ngơi / Tắt app | `Offline` | Cuối ngày hoặc muốn tạm dừng |
| Hệ thống auto | `Busy` | Khi được assign shipment (không cần gọi API) |

### 6.3 Xem shipment đang được giao

```http
GET /api/v1/shipments/{shipmentId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "c4d5e6f7-...",
    "orderId": "a1b2c3d4-...",
    "status": "InProgress",
    "assignedDriverId": "7b2f4c8e-...",
    "assignedVehicleId": "a1b2c3d4-...",
    "pickupAddress": {
      "street": "123 Nguyễn Huệ",
      "city": "TP. Hồ Chí Minh",
      "province": "Hồ Chí Minh"
    },
    "deliveryAddress": {
      "street": "456 Lê Lợi",
      "city": "Hà Nội",
      "province": "Hà Nội"
    },
    "packages": [
      {
        "productName": "Tủ lạnh Samsung",
        "weightKg": 45.0,
        "lengthM": 0.6,
        "widthM": 0.7,
        "heightM": 1.8
      }
    ],
    "createdAt": "2026-04-30T08:00:00Z"
  }
}
```

---

## 7. Active Delivery — Màn hình giao hàng

### 7.1 GPS Push Loop

Gửi vị trí định kỳ khi đang giao hàng:

```http
POST /api/v1/tracking/location
Authorization: Bearer <token>

{
  "shipmentId": "c4d5e6f7-...",
  "latitude": 10.7769,
  "longitude": 106.7009
}
```

Response: `200 OK` hoặc `204 No Content`

**Adaptive interval (tiết kiệm battery):**

| Trạng thái xe | Interval | Logic |
|---|---|---|
| Đang di chuyển (speed > 5km/h) | 1–2 giây | GPS thay đổi nhanh |
| Dừng ngắn (speed ≤ 5km/h, < 30s) | 5 giây | Có thể đèn đỏ |
| Dừng lâu (> 30s bất động) | 15–30 giây | Đang bốc/dỡ hàng |
| Shipment = `Delivered` hoặc `Completed` | Dừng hẳn | Không cần push nữa |

**Offline handling:** Cache các location points khi mất kết nối, gửi batch khi có mạng lại (tối đa 100 points/batch).

### 7.2 Cập nhật trạng thái shipment

```http
PUT /api/v1/shipments/{shipmentId}/status
Authorization: Bearer <token>

{
  "status": "PickedUp"
}
```

**Hành trình trạng thái cho driver:**

```
InProgress (hệ thống set khi bắt đầu)
     ↓  [Driver đến nơi, lấy hàng xong]
  PickedUp
     ↓  [Driver bắt đầu chạy]
  InTransit
     ↓  [Driver giao hàng, khách ký nhận]
  Delivered
```

| Màn hình | Action của driver | Status gửi lên |
|---|---|---|
| Đến nơi lấy hàng, đã lấy xong | Nhấn "Đã lấy hàng" | `PickedUp` |
| Đang chạy trên đường | Nhấn "Bắt đầu giao" | `InTransit` |
| Đến nơi giao, khách ký nhận | Nhấn "Đã giao thành công" | `Delivered` |

### 7.3 Xem lịch sử vị trí (debug / review)

```http
GET /api/v1/tracking/shipments/{shipmentId}/points
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": [
    { "latitude": 10.7769, "longitude": 106.7009, "recordedAt": "2026-04-30T08:05:00Z" },
    { "latitude": 10.7812, "longitude": 106.6987, "recordedAt": "2026-04-30T08:06:00Z" }
  ]
}
```

---

## 8. Báo cáo hỏng xe (Breakdown)

### 8.1 Luồng báo hỏng xe

```
1. Driver nhấn "Báo hỏng xe"
2. App yêu cầu chụp ≥1 ảnh hỏng hóc
3. App lấy GPS hiện tại
4. POST /api/v1/drivers/{driverId}/report-breakdown
5. Hệ thống Anti-Fraud Gate kiểm tra:
   - TrustScore ≥ 30?
   - Có ≥1 ảnh?
   - GPS có khớp với vị trí tracking gần nhất không?
6. Nếu pass → Shipment vào trạng thái Reassigning
7. Hệ thống tự tìm driver mới (Breakdown Saga)
```

### 8.2 API báo hỏng xe

**Trước khi gọi API, upload ảnh hỏng xe qua pre-signed URL:**

```http
GET /api/v1/uploads/presigned-url?type=breakdown-photo&count=2
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "urls": [
      "http://minio:9000/breakdown-photos/uuid1.jpg?X-Amz-Signature=...",
      "http://minio:9000/breakdown-photos/uuid2.jpg?X-Amz-Signature=..."
    ]
  }
}
```

Upload từng ảnh với `PUT <url>`, sau đó dùng phần path của URL (e.g. `breakdown-photos/uuid1.jpg`) làm giá trị trong `photoUrls`.

```http
POST /api/v1/drivers/{driverId}/report-breakdown
Authorization: Bearer <token>     (role=Driver)
Content-Type: application/json

{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "photoUrls": [
    "breakdown-photos/uuid1.jpg",
    "breakdown-photos/uuid2.jpg"
  ],
  "description": "Xe bị nổ lốp, không thể tiếp tục"
}
```

```json
{
  "success": true,
  "data": {
    "breakdownReportId": "d5e6f7a8-...",
    "riskLevel": "Low",
    "message": "Đã ghi nhận sự cố. Hệ thống đang tìm tài xế thay thế."
  }
}
```

**Trường hợp bị từ chối (422 Unprocessable Entity):**

```json
{
  "success": false,
  "error": {
    "code": "BREAKDOWN_GATE_REJECTED",
    "message": "Yêu cầu không hợp lệ: TrustScore quá thấp hoặc thiếu ảnh chứng minh"
  }
}
```

**`riskLevel` enum:**

| Level | Điều kiện |
|---|---|
| `Low` | GPS của driver khớp với tracking hiện tại (≤2km) |
| `Medium` | GPS cách xa tracking hiện tại (>2km) |
| `High` | Nhiều yếu tố bất thường |

**Lưu ý cho UI:** Luôn upload ảnh lên MinIO trước, lấy pre-signed URL qua `?type=breakdown-photo&count=N` (1–10 ảnh). Tối thiểu 1 ảnh, Anti-Fraud Gate sẽ reject nếu `photoUrls` rỗng.

---

## 9. Real-time qua SignalR (`/hubs/tracking`)

### 9.1 Kết nối

```dart
// Flutter example (signalr_netcore package)
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

```kotlin
// Android (Kotlin) — microsoft-signalr library
val connection = HubConnectionBuilder
  .create("https://api.example.com/hubs/tracking")
  .withAccessTokenProvider { getAccessToken() }
  .build()
connection.start().blockingAwait()
```

### 9.2 Subscribe vào Driver Group

Sau khi kết nối, join group để nhận events cho tài xế này:

```dart
await connection.invoke("JoinDriverGroup", args: [driverId]);
```

### 9.3 Events nhận từ server

#### Nhận assignment mới

```dart
connection.on("DriverAssigned", (args) {
  // args[0] là JSON object:
  // {
  //   "shipmentId": "c4d5e6f7-...",
  //   "orderId": "a1b2c3d4-...",
  //   "pickupAddress": { "street": "...", "city": "...", "province": "..." },
  //   "deliveryAddress": { "street": "...", "city": "...", "province": "..." },
  //   "packages": [
  //     { "productName": "...", "weightKg": 45.0, "lengthM": 0.6, ... }
  //   ],
  //   "estimatedDistance": 12.5,   // km (nếu có)
  //   "estimatedDuration": 35      // phút (nếu có)
  // }
  showAssignmentNotification(args[0]);
});
```

#### Shipment bị reassign (hỏng xe của driver khác → nhận lại)

```dart
connection.on("ShipmentReassigned", (args) {
  // args[0]: { "shipmentId": "...", "reason": "Xe trước bị hỏng" }
  showReassignmentDialog(args[0]);
});
```

### 9.4 Xử lý reconnect

```dart
connection.onreconnecting((error) {
  showReconnectingBanner();
});

connection.onreconnected((connectionId) async {
  hideBanner();
  // Re-join group sau khi reconnect
  await connection.invoke("JoinDriverGroup", args: [driverId]);
});

connection.onclose((error) {
  showOfflineBanner();
  // Trigger manual reconnect sau 5 giây
  Future.delayed(Duration(seconds: 5), () => connection.start());
});
```

---

## 10. Push Notifications (FCM)

### 10.1 Đăng ký FCM token

Sau khi login, gửi FCM token lên server:

```http
POST /api/v1/notifications/register-device
Authorization: Bearer <token>

{
  "token": "fcm-token-here...",
  "platform": "Android"     // "Android" | "Ios"
}
```

### 10.2 Các notification driver nhận được

| Trigger | Notification | Action khi tap |
|---|---|---|
| Được assign shipment | "Bạn có đơn hàng mới" + địa chỉ lấy | Mở màn hình delivery |
| Admin confirm dispatch (sau bin-check) | "Đơn đã được duyệt, bắt đầu giao" | Mở màn hình delivery |
| Shipment bị reassign về | "Có đơn chuyển đến bạn từ xe gặp sự cố" | Mở màn hình delivery |
| Verification completed | "Xác minh hoàn tất: [OcrVerified / ManualReview / Rejected]" | Mở màn hình profile |

### 10.3 Notification payload format

```json
{
  "notification": {
    "title": "Đơn hàng mới",
    "body": "Lấy hàng tại 123 Nguyễn Huệ, TP.HCM"
  },
  "data": {
    "type": "DRIVER_ASSIGNED",
    "shipmentId": "c4d5e6f7-...",
    "orderId": "a1b2c3d4-..."
  }
}
```

---

## 11. Profile & Verification Status

### 11.1 Xem thông tin verification

```http
GET /api/v1/drivers/{driverId}
Authorization: Bearer <token>
```

Xem field `verificationStatus` trong response (mục 6.1).

### 11.2 Màn hình trạng thái xác minh

```
┌──────────────────────────────────────────┐
│  Trạng thái hồ sơ                        │
│                                          │
│  CCCD/CMND      ✅ Đã xác minh           │
│  GPLX           ✅ Đã xác minh           │
│  Đăng ký xe     ⚠️  Đang xem xét thủ công │
│                                          │
│  Overall: ManualReview                   │
│  "Hồ sơ đang được Admin xem xét.        │
│   Vui lòng chờ 1-2 ngày làm việc."      │
│                                          │
│  [Liên hệ hỗ trợ]                        │
└──────────────────────────────────────────┘
```

### 11.3 TrustScore

TrustScore hiển thị trong profile (0–100, default 70):

- Mỗi lần báo hỏng xe: -3 điểm
- Collusion bị phát hiện (swap với cùng 1 tài xế > 3 lần): -10 điểm
- Driver cần duy trì TrustScore ≥ 30 để báo hỏng xe

---

## 12. Escrow Payment (khi nhận đơn từ xe hỏng)

Nếu driver nhận đơn từ xe khác bị hỏng (reassignment), một khoản phụ phí escrow 50.000 VNĐ có thể được áp dụng:

```http
GET /api/v1/payments/escrow/{escrowId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "...",
    "shipmentId": "...",
    "amount": 50000,
    "status": "Locked",
    "createdAt": "2026-04-30T09:00:00Z"
  }
}
```

---

## 13. Offline & Resilience

| Tình huống | Xử lý đề xuất |
|---|---|
| Mất mạng khi push GPS | Cache tối đa 100 points locally, gửi batch khi có mạng lại |
| Token hết hạn giữa chuyến | Auto-refresh silent, không interrupt delivery |
| SignalR disconnect | `withAutomaticReconnect()` + rejoin group sau reconnect |
| Shipment status update thất bại | Retry 3 lần với exponential backoff (1s, 3s, 9s) |
| OCR extract timeout | Timeout sau 30s, cho phép manual fill form |
| Mất mạng khi upload ảnh | Retry với resumable upload; hiển thị progress bar |

---

## 14. Error Codes

| Code | HTTP | Ý nghĩa | Xử lý |
|---|---|---|---|
| `DRIVER_NOT_VERIFIED` | 403 | Driver chưa được xác minh | Chuyển sang màn hình verification |
| `DRIVER_NOT_FOUND` | 404 | driverId không tồn tại | Log, show lỗi generic |
| `BREAKDOWN_GATE_REJECTED` | 422 | Anti-fraud gate reject | Show lý do cụ thể |
| `INVALID_STATUS_TRANSITION` | 422 | Chuyển trạng thái không hợp lệ | Reload shipment, show lỗi |
| `SHIPMENT_NOT_FOUND` | 404 | shipmentId không tồn tại | Reload dashboard |
| `UNAUTHORIZED` | 401 | Token hết hạn hoặc không hợp lệ | Auto-refresh hoặc re-login |
| `FORBIDDEN` | 403 | Role không có quyền | Show lỗi rõ ràng |
| `VALIDATION_ERROR` | 400 | Dữ liệu gửi không hợp lệ | Hiển thị lỗi theo field |
| `DOMAIN_ERROR` | 422 | Lỗi business logic | Hiển thị message từ server |
| `SERVER_ERROR` | 500 | Lỗi nội bộ | Retry hoặc báo support |

---

## 15. Checklist tích hợp

- [ ] Implement auth flow (register → login → refresh token)
- [ ] Implement onboarding 3 bước
- [ ] Integrate PaddleOCR auto-fill (3 endpoints)
- [ ] Implement pre-signed URL upload flow
- [ ] Implement GPS push loop với adaptive interval
- [ ] Connect SignalR và handle `DriverAssigned` event
- [ ] Implement shipment status update
- [ ] Implement breakdown report flow
- [ ] Register FCM token sau login
- [ ] Handle notification tap → navigate to correct screen
- [ ] Implement offline GPS caching
- [ ] Handle token expiry (silent refresh)
- [ ] Handle SignalR reconnect + group rejoin
