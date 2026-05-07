# Luồng đăng ký tài khoản Tài xế

> **Dành cho:** Team Mobile — Driver App  
> **Cập nhật:** 2026-05-07  
> **Base URL:** `https://<gateway-host>:8080`

---

## Tổng quan

Đăng ký tài khoản driver gồm **3 bước tuần tự**. Bước 2 (upload ảnh) và Bước 3 (nộp hồ sơ) yêu cầu phải đăng nhập trước.

```
Bước 1 ── Tạo tài khoản ──────────────────────► userId + login
Bước 2 ── Upload 7 ảnh giấy tờ lên MinIO ─────► 7 file URL
Bước 3 ── Nộp hồ sơ tài xế + xe ──────────────► driverId + vehicleId

        (Backend tự động)
           └── OCR xác minh ảnh
                  ├── Score ≥ 0.85 → OcrVerified   ► Tài xế set Available
                  ├── Score 0.65–0.85 → ManualReview ► Admin duyệt
                  └── Score < 0.65  → Rejected
```

---

## Bước 1 — Tạo tài khoản

### Request

```http
POST /api/v1/auth/register/driver
Content-Type: application/json
```

```json
{
  "email": "driver@example.com",
  "password": "Abc12345!",
  "firstName": "Văn A",
  "lastName": "Nguyễn",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1995-06-15"
}
```

| Trường | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `email` | string | ✅ | Địa chỉ email hợp lệ, tối đa 256 ký tự |
| `password` | string | ✅ | Tối thiểu 8 ký tự, tối đa 128 ký tự |
| `firstName` | string | ✅ | Tối đa 100 ký tự |
| `lastName` | string | ✅ | Tối đa 100 ký tự |
| `phoneNumber` | string | ✅ | Định dạng Việt Nam: `+84xxxxxxxxx` hoặc `0xxxxxxxxx` (9 số sau đầu số, đầu số từ 3–9) |
| `dateOfBirth` | string (ISO 8601) | ✅ | Phải đủ **18 tuổi** |

### Response thành công `201`

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "driver@example.com"
}
```

### Lỗi thường gặp

| HTTP | Code | Nguyên nhân |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Sai định dạng phone, dưới 18 tuổi, password quá ngắn |
| 409 | `User.Email.Conflict` | Email đã được đăng ký |

---

## Đăng nhập — Lấy token cho Bước 2 & 3

```http
POST /api/v1/auth/login
Content-Type: application/json
```

```json
{
  "email": "driver@example.com",
  "password": "Abc12345!"
}
```

### Response thành công `200`

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "base64string...",
  "expiresAt": "2026-05-07T11:00:00Z",
  "userId": "550e8400-...",
  "role": "Driver"
}
```

> **Lưu ý lưu trữ:** Lưu `accessToken` và `refreshToken` vào Secure Storage (iOS Keychain / Android Keystore). Token hết hạn sau 60 phút — dùng `POST /api/v1/auth/refresh` để gia hạn.

---

## Bước 2 — Upload ảnh giấy tờ lên MinIO

### 2.1 Lấy presigned URL

```http
GET /api/v1/uploads/presigned-url?type=driver-document
Authorization: Bearer <accessToken>
```

### Response `200`

```json
{
  "portraitUrl":        "https://minio.internal/driver-documents/uuid-portrait.jpg?X-Amz-Signature=...",
  "idCardFrontUrl":     "https://minio.internal/driver-documents/uuid-cccd-front.jpg?X-Amz-Signature=...",
  "idCardBackUrl":      "https://minio.internal/driver-documents/uuid-cccd-back.jpg?X-Amz-Signature=...",
  "licenseFrontUrl":    "https://minio.internal/driver-documents/uuid-gplx-front.jpg?X-Amz-Signature=...",
  "licenseBackUrl":     "https://minio.internal/driver-documents/uuid-gplx-back.jpg?X-Amz-Signature=...",
  "vehicleRegFrontUrl": "https://minio.internal/driver-documents/uuid-dkx-front.jpg?X-Amz-Signature=...",
  "vehicleRegBackUrl":  "https://minio.internal/driver-documents/uuid-dkx-back.jpg?X-Amz-Signature=..."
}
```

### 2.2 Upload từng ảnh

Với **mỗi URL** trên, thực hiện HTTP PUT trực tiếp đến MinIO:

```http
PUT <presignedUrl>
Content-Type: image/jpeg

<binary image data>
```

- Response thành công: `200 OK` (body rỗng)
- URL có hiệu lực trong **15 phút** kể từ khi lấy — phải upload trong thời gian đó
- Sau khi PUT thành công, **lưu lại 7 URL** (bỏ phần query string `?X-Amz-...`) để dùng ở Bước 3

> **Gợi ý UX:** Cho phép tài xế chụp ảnh lần lượt từng loại giấy tờ, upload song song để tăng tốc độ. Hiển thị tiến trình upload (7/7).

---

## Bước 3 — Nộp hồ sơ tài xế và xe

### Request

```http
POST /api/v1/drivers/register
Authorization: Bearer <accessToken>
Content-Type: application/json
```

```json
{
  "idCardNumber": "079095012345",
  "dateOfBirth": "1995-06-15",
  "address": "123 Lê Lợi, Quận 1, TP.HCM",
  "licenseNumber": "012345678901",
  "licenseGrade": 3,
  "licenseExpiryDate": "2030-06-15",
  "photos": {
    "portraitUrl":        "https://minio.internal/driver-documents/uuid-portrait.jpg",
    "idCardFrontUrl":     "https://minio.internal/driver-documents/uuid-cccd-front.jpg",
    "idCardBackUrl":      "https://minio.internal/driver-documents/uuid-cccd-back.jpg",
    "licenseFrontUrl":    "https://minio.internal/driver-documents/uuid-gplx-front.jpg",
    "licenseBackUrl":     "https://minio.internal/driver-documents/uuid-gplx-back.jpg",
    "vehicleRegFrontUrl": "https://minio.internal/driver-documents/uuid-dkx-front.jpg",
    "vehicleRegBackUrl":  "https://minio.internal/driver-documents/uuid-dkx-back.jpg"
  },
  "vehicle": {
    "licensePlate": "51F-123.45",
    "brand": "Hyundai",
    "model": "Porter II",
    "type": 3,
    "maxWeightKg": 1000,
    "maxVolumeCbm": 5.0,
    "lengthM": 3.1,
    "widthM": 1.6,
    "heightM": 1.7,
    "yearOfManufacture": 2020,
    "registrationNumber": "HCM-20-12345",
    "registrationExpiryDate": "2028-12-31"
  }
}
```

> **Lưu ý:** `firstName`, `lastName`, `phoneNumber`, `email` được backend tự đọc từ JWT — **không cần gửi lại** trong body.

### Giải thích các trường

#### Thông tin tài xế

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `idCardNumber` | string | Số CCCD/CMND, unique trong hệ thống |
| `dateOfBirth` | string (YYYY-MM-DD) | Phải trùng với Bước 1 |
| `address` | string | Địa chỉ thường trú |
| `licenseNumber` | string | Số GPLX |
| `licenseGrade` | int | Xem bảng enum bên dưới |
| `licenseExpiryDate` | string (YYYY-MM-DD) | Phải còn hạn (> ngày hiện tại) |

#### Enum `licenseGrade`

| Giá trị | Tên | Ghi chú |
|---|---|---|
| 2 | B2 | Xe con dưới 9 chỗ |
| 3 | C | Xe tải dưới 3.5 tấn ✅ |
| 4 | D | Xe khách 10–30 chỗ |
| 6 | FC | Xe tải nặng + kéo rơmoóc ✅ |
| 7 | FD | Xe tải nặng chuyên dụng ✅ |

> ⚠️ **B1 (1) và E (5) bị từ chối** — không đủ điều kiện vận chuyển hàng hóa.

#### Thông tin xe (`vehicle`)

| Trường | Kiểu | Ràng buộc |
|---|---|---|
| `licensePlate` | string | Unique trong hệ thống, tự động uppercase |
| `brand` | string | Hãng xe |
| `model` | string | Model xe |
| `type` | int | Xem bảng enum bên dưới |
| `maxWeightKg` | decimal | Phải > 0 |
| `maxVolumeCbm` | decimal | Phải > 0 (m³) |
| `lengthM` | decimal | Phải > 0 (m) |
| `widthM` | decimal | Phải > 0 (m) |
| `heightM` | decimal | Phải > 0 (m) |
| `yearOfManufacture` | int | Năm sản xuất |
| `registrationNumber` | string | Số đăng ký xe |
| `registrationExpiryDate` | string (YYYY-MM-DD) | Phải còn hạn |

#### Enum `vehicle.type`

| Giá trị | Tên |
|---|---|
| 1 | Motorbike |
| 2 | Van |
| 3 | Truck3T |
| 4 | Truck5T |
| 5 | Truck10T |
| 6 | Truck15T |

### Response thành công `201`

```json
{
  "driverId": "550e8400-e29b-41d4-a716-446655440000",
  "vehicleId": "660f9511-f30c-52e5-b827-557766551111",
  "verificationStatus": "PendingOcrVerification"
}
```

**Lưu `driverId` và `vehicleId`** — dùng cho các API tiếp theo.

### Lỗi thường gặp

| HTTP | Code | Nguyên nhân |
|---|---|---|
| 400 | `VALIDATION_ERROR` | Dữ liệu không hợp lệ (GPLX hết hạn, đăng ký xe hết hạn, dimensions = 0...) |
| 400 | `Validation.Driver.LicenseGrade` | Hạng bằng B1 hoặc E — không được phép |
| 409 | `Driver.Conflict` | `driverId` đã tồn tại (đã nộp hồ sơ trước đó) |
| 409 | `Driver.IdCard.Conflict` | Số CCCD đã được đăng ký bởi tài khoản khác |
| 409 | `Vehicle.Conflict` | Biển số xe đã được đăng ký |

---

## Sau Bước 3 — Trạng thái xác minh

Sau khi nộp hồ sơ, backend OCR tự động xử lý (thường mất **30 giây – 2 phút**).

### Các trạng thái (`verificationStatus`)

| Giá trị | Ý nghĩa | Hành động tiếp theo |
|---|---|---|
| `PendingOcrVerification` (1) | Đang chờ OCR xử lý | Polling hoặc chờ push notification |
| `OcrVerified` (2) | OCR xác minh thành công | Tài xế có thể set trạng thái Available |
| `ManualReview` (3) | OCR không chắc chắn, cần Admin duyệt | Chờ thông báo từ Admin |
| `AdminVerified` (4) | Admin đã duyệt | Tài xế có thể set trạng thái Available |
| `Rejected` (5) | Bị từ chối | Hiển thị thông báo, cho phép nộp lại |

### Polling trạng thái xác minh

App nên poll mỗi **30 giây** sau khi nộp hồ sơ:

```http
GET /api/v1/drivers/{driverId}
Authorization: Bearer <accessToken>
```

Kiểm tra field `verificationStatus` trong response. Dừng poll khi nhận được `OcrVerified`, `AdminVerified`, hoặc `Rejected`.

### Push notification (thay thế cho polling)

Đăng ký FCM device token để nhận thông báo khi trạng thái thay đổi:

```http
POST /api/v1/notifications/register-device
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "deviceToken": "fcm-device-token-string",
  "platform": "android"
}
```

`platform`: `"android"` hoặc `"ios"`

---

## Bắt đầu nhận đơn (sau khi verified)

Sau khi `verificationStatus` là `OcrVerified` hoặc `AdminVerified`, tài xế tự set trạng thái:

```http
PUT /api/v1/drivers/{driverId}/status
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "status": 2
}
```

`status`: `1` = Offline, `2` = Available, `3` = Busy

> ⚠️ Set Available khi chưa verified sẽ trả về lỗi `400 Validation.Driver.Verification`.

---

## Refresh Token

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "userId": "550e8400-...",
  "refreshToken": "base64string..."
}
```

- Mỗi lần refresh → token cũ **bị hủy ngay lập tức** (rotation)
- Refresh token TTL: **30 ngày**
- Khuyến nghị: tự động refresh khi access token còn dưới 60 giây

---

## Sơ đồ luồng đầy đủ

```
[Driver App]                    [Backend]

1. POST /auth/register/driver
   ─────────────────────────► Tạo User (Role=Driver)
   ◄──────── { userId } ──────

2. POST /auth/login
   ─────────────────────────► Xác thực credentials
   ◄──── { accessToken } ─────

3. GET /uploads/presigned-url
   ─────────────────────────► Tạo 7 presigned URL
   ◄───── { 7 URLs } ─────────

4. PUT <presignedUrl> × 7
   ──────────────────────────────────────► MinIO lưu ảnh
   ◄──── 200 OK × 7 ──────────────────────

5. POST /drivers/register
   ─────────────────────────► Tạo Driver + Vehicle
                               Link vehicle ↔ driver
                               Publish VehicleAssignedToDriverEvent
                               Publish DriverDocumentsSubmittedEvent
   ◄── { driverId, vehicleId, ─
         PendingOcrVerification }

6. (Tự động) OCR Service xử lý ảnh
                               ┌── Score ≥ 0.85 → OcrVerified
                               ├── Score 0.65–0.85 → ManualReview
                               └── Score < 0.65  → Rejected

7. GET /drivers/{id}  (polling)
   ─────────────────────────► Trả verificationStatus
   ◄───── { verificationStatus }

8. PUT /drivers/{id}/status  { "status": 2 }
   ─────────────────────────► Driver = Available
                              ► Sẵn sàng nhận đơn
```

---

## Checklist tích hợp

- [ ] Gọi đúng endpoint `/api/v1/auth/register/driver` (không phải `/register` chung)
- [ ] `dateOfBirth` gửi định dạng `YYYY-MM-DD`
- [ ] `licenseGrade` và `vehicle.type` gửi **số nguyên** (int), không phải string
- [ ] Lưu presigned URL và upload trong vòng 15 phút
- [ ] Lưu `driverId` sau Bước 3 để dùng cho các API tiếp theo
- [ ] Implement polling hoặc FCM để theo dõi trạng thái xác minh
- [ ] Xử lý trường hợp `ManualReview` — UI hiển thị "Đang chờ Admin duyệt"
- [ ] Xử lý trường hợp `Rejected` — cho phép tài xế nộp lại hồ sơ
