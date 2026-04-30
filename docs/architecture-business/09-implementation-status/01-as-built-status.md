# As-Built Status — Design vs. Implementation

> Cập nhật: 2026-04-30 | Khảo sát toàn bộ src/ vs. business requirements
>
> Legend: ✅ Done | ⚠️ Partial | ❌ Missing | 🐛 Bug

---

## 1. Services Overview

| Service | Port | Status | Ghi chú quan trọng |
|---|---|---|---|
| API Gateway | :8080 | ⚠️ | Thiếu vehicle-route, analytics-route, **ocr-route** |
| Identity | :8081 | ⚠️ | Thiếu phone, DOB trong User; thiếu role trong Register |
| Order | :8082 | 🐛 | **Không có Consumers** — status không bao giờ tự update |
| Driver/Vehicle | :8083 | ⚠️ | Thiếu LicenseGrade, DOB, address, **7 photo URLs, VerificationStatus** trong Driver; thiếu dims trong Vehicle |
| Route (Rust) | :8084 | ✅ | A\*, Haversine, Redis cache |
| Optimizer (Python) | :8085 | ⚠️ | Chưa filter theo LicenseGrade |
| Shipment | :8086 | ⚠️ | Thiếu list query, thiếu decline-dispatch |
| Tracking | :8087 | ⚠️ | Thiếu `DriverAssigned` SignalR event |
| Notification | :8088 | ⚠️ | Thiếu REST API (FCM token), senders là stub |
| Payment | :8089 | ⚠️ | Thiếu list query, escrow lookup |
| Analytics | :8095 | ⚠️ | Code ✅ nhưng Gateway route ❌ |
| **OCR** | **:8090** | **❌** | **Chưa tồn tại** — Python/FastAPI/PaddleOCR service cần tạo |

---

## 2. Domain Models — As-Built vs. Required

### 2.1 User Aggregate (Identity)

**File:** `src/Services/Identity/TruckDelivery.Identity.Domain/Aggregates/User.cs`

| Field | Status | Ghi chú |
|---|---|---|
| Email | ✅ | |
| PasswordHash | ✅ | BCrypt |
| FirstName, LastName | ✅ | |
| Role (Customer/Driver/Admin) | ✅ | |
| IsActive | ✅ | |
| RefreshToken | ✅ | |
| PhoneNumber | ❌ | Bắt buộc cho Customer (liên lạc giao hàng) |
| DateOfBirth | ❌ | Optional — KYC cơ bản |
| CreatedAt, LastLoginAt | ✅ | |

**RegisterRequest controller** — `AuthController.cs:16`:
```csharp
// HIỆN TẠI:
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);

// CẦN:
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,          // MỚI — bắt buộc
    DateOnly? DateOfBirth = null); // MỚI — optional
// NOTE: Không có Role field — Customer role hardcoded trong handler
```

---

### 2.2 Driver Aggregate

**File:** `src/Services/Driver/TruckDelivery.Driver.Domain/Aggregates/Driver.cs`

| Field | Status | Ghi chú |
|---|---|---|
| Email | ✅ | |
| FirstName, LastName | ✅ | |
| PhoneNumber | ✅ | |
| LicenseNumber | ✅ | Số bằng lái |
| LicenseGrade | ❌ | **Quan trọng** — hạng bằng (B1/B2/C/D/E/FC/FD) |
| LicenseExpiryDate | ❌ | Hạn bằng lái — cần check trước khi assign |
| DateOfBirth | ❌ | Ngày sinh |
| Address | ❌ | Địa chỉ thường trú |
| IdCardNumber | ❌ | Số CCCD — unique, bắt buộc |
| PortraitPhotoUrl | ❌ | Ảnh chân dung |
| IdCardFrontUrl | ❌ | Ảnh CCCD mặt trước |
| IdCardBackUrl | ❌ | Ảnh CCCD mặt sau |
| LicenseFrontUrl | ❌ | Ảnh GPLX mặt trước |
| LicenseBackUrl | ❌ | Ảnh GPLX mặt sau |
| VehicleRegFrontUrl | ❌ | Ảnh đăng ký xe mặt trước |
| VehicleRegBackUrl | ❌ | Ảnh đăng ký xe mặt sau |
| VerificationStatus | ❌ | DriverVerificationStatus enum — kiểm soát khi nào driver được set Available |
| OcrConfidenceScore | ❌ | Score từ OCR service (0.0–1.0) |
| VerificationNotes | ❌ | Ghi chú Admin khi verify/reject thủ công |
| Status (Offline/Available/Busy/Suspended) | ✅ | |
| CurrentVehicleId | ✅ | |
| TrustScore | ✅ | 0–100, default 70 |
| IsActive | ✅ | |

**RegisterDriverCommand** — thiếu tất cả fields mới:
```csharp
// HIỆN TẠI:
public sealed record RegisterDriverCommand(
    Guid UserId, string Email, string FirstName, string LastName,
    string PhoneNumber, string LicenseNumber) : IRequest<Result>;

// CẦN:
public sealed record RegisterDriverCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Address,           // MỚI
    string LicenseNumber,
    LicenseGrade LicenseGrade,       // MỚI
    DateOnly LicenseExpiryDate,      // MỚI
    DateOnly DateOfBirth) : IRequest<Result>; // MỚI
```

**DriverDto** thiếu `TrustScore` và các fields mới:
```csharp
// src/Services/Driver/TruckDelivery.Driver.Application/DTOs/DriverDto.cs
// HIỆN TẠI thiếu: TrustScore, LicenseGrade, DateOfBirth, Address
```

---

### 2.3 Vehicle Aggregate

**File:** `src/Services/Driver/TruckDelivery.Driver.Domain/Aggregates/Vehicle.cs`

| Field | Status | Ghi chú |
|---|---|---|
| LicensePlate | ✅ | |
| Brand | ✅ | |
| Model | ✅ | |
| Type (VehicleType enum) | ✅ | |
| MaxWeightKg | ✅ | |
| MaxVolumeCbm | ✅ | |
| YearOfManufacture | ✅ | |
| Status | ✅ | |
| AssignedDriverId | ✅ | |
| LengthM | ❌ | Kích thước khoang hàng — cần cho bin-check |
| WidthM | ❌ | Kích thước khoang hàng — cần cho bin-check |
| HeightM | ❌ | Kích thước khoang hàng — cần cho bin-check |
| RegistrationNumber | ❌ | Số giấy đăng ký xe |
| RegistrationExpiryDate | ❌ | Hạn đăng kiểm |

> ⚠️ `frontend-integration.md` document `lengthM`, `widthM`, `heightM` trong Vehicle response nhưng chúng không tồn tại trong aggregate hay DTO — **đây là discrepancy nghiêm trọng** vì bin-check service cần dimensions.

**RegisterVehicleCommand** thiếu dimensions:
```csharp
// HIỆN TẠI:
public sealed record RegisterVehicleCommand(
    string LicensePlate, string Brand, string Model, VehicleType Type,
    decimal MaxWeightKg, decimal MaxVolumeCbm, int YearOfManufacture) : IRequest<Result<Guid>>;

// CẦN thêm:
decimal LengthM,
decimal WidthM,
decimal HeightM,
string RegistrationNumber,
DateOnly RegistrationExpiryDate
```

---

### 2.4 LicenseGrade Enum — Chưa tồn tại

```
src/Services/Driver/TruckDelivery.Driver.Domain/ValueObjects/LicenseGrade.cs  ← CHƯA CÓ
```

Cần tạo mới:
```csharp
public enum LicenseGrade
{
    B1 = 1,  // Xe con không kinh doanh — KHÔNG đủ điều kiện
    B2 = 2,  // Van, motorbike kinh doanh vận tải
    C  = 3,  // Xe tải ≥3.5T
    D  = 4,  // Xe khách 10–30 chỗ
    E  = 5,  // Xe khách >30 chỗ — KHÔNG áp dụng cho truck delivery
    FC = 6,  // Xe đầu kéo, container, Truck15T
    FD = 7   // Phương tiện kết hợp hạng nặng
}
```

**LicenseGrade → VehicleType compatibility:**

| LicenseGrade | Eligible VehicleTypes |
|---|---|
| B1 | ❌ Không eligible |
| B2 | Motorbike, Van |
| C | Truck3T, Truck5T, Truck10T |
| D | Van (edge case) |
| E | ❌ Không applicable |
| FC | Truck15T |
| FD | Truck15T |

### 2.5 DriverVerificationStatus Enum — Chưa tồn tại

```
src/Services/Driver/TruckDelivery.Driver.Domain/ValueObjects/DriverVerificationStatus.cs  ← CHƯA CÓ
```

Cần tạo mới:
```csharp
public enum DriverVerificationStatus
{
    Draft                  = 0,  // Chưa nộp hồ sơ
    PendingOcrVerification = 1,  // Đã nộp, đang chờ OCR async
    OcrVerified            = 2,  // OCR pass — driver được phép set Available
    ManualReview           = 3,  // OCR flagged — Admin xem xét
    AdminVerified          = 4,  // Admin xác nhận — driver được phép set Available
    Rejected               = 5   // Bị từ chối — phải upload lại
}
```

**Guard quan trọng:** `Driver.UpdateStatus(Available)` phải fail khi `VerificationStatus` không phải `OcrVerified` hoặc `AdminVerified`.

---

## 2.6 OCR Service — Chưa tồn tại

| Hạng mục | Status | Ghi chú |
|---|---|---|
| Python service structure | ❌ | Tạo tại `src/Services/OCR/truck-delivery-ocr/` |
| PaddleOCR integration | ❌ | Xem `docs/architecture-business/10-ocr-verification/01-ocr-service.md` |
| `POST /api/v1/ocr/extract/id-card` | ❌ | Phase A auto-fill |
| `POST /api/v1/ocr/extract/license` | ❌ | Phase A auto-fill |
| `POST /api/v1/ocr/extract/vehicle-reg` | ❌ | Phase A auto-fill |
| Kafka consumer: `driver.documents.submitted` | ❌ | Phase B async verify |
| Kafka producer: `ocr.driver.verification-completed` | ❌ | Phase B result |
| Gateway route `/api/v1/ocr/*` → ocr-cluster | ❌ | Port :8090 |
| Docker compose entry | ❌ | Cần thêm vào `docker/docker-compose.yml` |

---

## 3. Registration Flows — As-Built vs. Required

### 3.1 Customer Registration

| Step | Current | Required |
|---|---|---|
| Endpoint | `POST /api/v1/auth/register` ✅ | Giữ nguyên |
| Email + Password | ✅ | ✅ |
| FirstName + LastName | ✅ | ✅ |
| PhoneNumber | ❌ | Bắt buộc |
| DateOfBirth | ❌ | Optional |
| Role assignment | Hardcoded Customer trong handler (cần verify) | Hardcoded Customer ✅ |

### 3.2 Driver Registration

| Step | Current | Required |
|---|---|---|
| Bước 1: Tạo User account | ❌ Không có endpoint riêng | `POST /api/v1/auth/register/driver` |
| Bước 2: Upload ảnh + OCR auto-fill | ❌ Chưa có | `GET /api/v1/uploads/presigned-url` + 3 OCR extract endpoints |
| Bước 3: Submit hồ sơ đầy đủ | ❌ Chỉ có `POST /drivers` (Admin only) | `POST /api/v1/drivers/register` (Driver self-service) |
| LicenseGrade | ❌ | Bắt buộc + validation |
| LicenseExpiryDate | ❌ | Bắt buộc + check > today |
| DateOfBirth | ❌ | Bắt buộc |
| Address | ❌ | Bắt buộc |
| IdCardNumber | ❌ | Bắt buộc, unique |
| 7 photo URLs | ❌ | Bắt buộc (portrait, CCCD ×2, GPLX ×2, đăng ký xe ×2) |
| VerificationStatus | ❌ | Trả về `PendingOcrVerification` sau submit |
| Vehicle info (brand, model, plate) | `POST /api/v1/vehicles` (Admin only) | Bundled trong driver registration |
| Vehicle dimensions | ❌ | Bắt buộc (cho bin-check) |
| RegistrationNumber + ExpiryDate | ❌ | Bắt buộc |

### 3.3 Admin Account

| Step | Current | Required |
|---|---|---|
| Self-registration | Không có (correct) | Không cho phép ✅ |
| Seed data | ❌ Không có seeder | `AdminSeeder.cs` bắt buộc |
| Admin tạo Admin | ❌ Không có endpoint | `POST /api/v1/admin/accounts` (SuperAdmin) |

---

## 4. API Endpoints — As-Built

### 4.1 Identity (:8081)

| Method | Path | Auth | Status |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Anonymous | ⚠️ thiếu phone/DOB |
| POST | `/api/v1/auth/register/driver` | Anonymous | ❌ |
| POST | `/api/v1/auth/login` | Anonymous | ✅ |
| POST | `/api/v1/auth/refresh` | Anonymous | ✅ |
| POST | `/api/v1/admin/accounts` | SuperAdmin | ❌ |

### 4.2 Order (:8082)

| Method | Path | Auth | Status |
|---|---|---|---|
| POST | `/api/v1/orders` | Customer | ✅ |
| GET | `/api/v1/orders/{id}` | Bearer | ⚠️ thiếu ShipmentId |
| GET | `/api/v1/orders?status=&page=` | Bearer | ⚠️ thiếu filter/pagination meta |
| DELETE | `/api/v1/orders/{id}` | Customer | ✅ |

**Kafka Consumers: ❌ KHÔNG CÓ (critical bug)**

### 4.3 Driver/Vehicle (:8083)

| Method | Path | Auth | Status |
|---|---|---|---|
| POST | `/api/v1/drivers` | Admin | ✅ (Admin-only, correct) |
| POST | `/api/v1/drivers/register` | Driver | ❌ self-service missing |
| GET | `/api/v1/drivers/{id}` | Bearer | ⚠️ thiếu TrustScore, grade |
| GET | `/api/v1/drivers/available` | Bearer | ✅ |
| GET | `/api/v1/drivers?status=&page=` | Admin | ❌ |
| PUT | `/api/v1/drivers/{id}/status` | Bearer | 🐛 thiếu role guard |
| POST | `/api/v1/drivers/{id}/assign-vehicle` | Admin | ✅ |
| POST | `/api/v1/drivers/{id}/report-breakdown` | Driver | ✅ |
| POST | `/api/v1/vehicles` | Admin | ✅ (thiếu dims) |
| GET | `/api/v1/vehicles/{id}` | Bearer | ⚠️ thiếu dims |
| GET | `/api/v1/vehicles?status=&page=` | Admin | ❌ |
| PUT | `/api/v1/vehicles/{id}/status` | Admin | ❌ |

**Gateway:** route `/api/v1/vehicles/*` → **❌ thiếu** (không accessible)

### 4.4.1 Driver Verification Admin Endpoints

| Method | Path | Auth | Status |
|---|---|---|---|
| GET | `/api/v1/drivers/pending-verification` | Admin | ❌ |
| POST | `/api/v1/drivers/{id}/verify` | Admin | ❌ |
| POST | `/api/v1/drivers/{id}/reject-verification` | Admin | ❌ |

### 4.9 OCR Service (:8090)

| Method | Path | Auth | Status |
|---|---|---|---|
| POST | `/api/v1/ocr/extract/id-card` | Driver | ❌ Service chưa tồn tại |
| POST | `/api/v1/ocr/extract/license` | Driver | ❌ |
| POST | `/api/v1/ocr/extract/vehicle-reg` | Driver | ❌ |

### 4.10 Uploads (Driver service hoặc Gateway util)

| Method | Path | Auth | Status |
|---|---|---|---|
| GET | `/api/v1/uploads/presigned-url` | Driver | ❌ |

### 4.4 Shipment (:8086)

| Method | Path | Auth | Status |
|---|---|---|---|
| GET | `/api/v1/shipments/{id}` | Bearer | ✅ |
| GET | `/api/v1/shipments?status=&page=` | Bearer | ❌ |
| GET | `/api/v1/shipments/active?driverId=` | Driver | ❌ |
| POST | `/api/v1/shipments/{id}/confirm-dispatch` | Admin | ✅ |
| POST | `/api/v1/shipments/{id}/decline-dispatch` | Admin | ❌ |
| PUT | `/api/v1/shipments/{id}/status` | Admin,Driver | 🐛 no Driver status restriction |

**Dispatch flow:** `DispatcherReviewRequired` state ✅, confirm ✅, decline ❌

### 4.5 Tracking (:8087)

| Method | Path | Auth | Status |
|---|---|---|---|
| POST | `/api/v1/tracking/location` | Driver | ✅ |
| GET | `/api/v1/tracking/shipments/{id}/points` | Bearer | ✅ |
| WS | `/hubs/tracking` | Bearer | ✅ |

**SignalR Events:**
- `LocationUpdated` ✅
- `ShipmentStatusUpdated` ✅
- `DriverAssigned` ❌ (not emitted to driver group)
- `DispatcherConfirmationRequired` ✅

### 4.6 Notification (:8088)

| Method | Path | Auth | Status |
|---|---|---|---|
| POST | `/api/v1/notifications/device-tokens` | Bearer | ❌ |

**Consumers:** ShipmentStatusUpdated ✅, DriverAssigned ✅, PaymentCompleted ✅

**Senders:** Push/SMS/Email đều là **stub** — chỉ log, không gửi thực sự.

### 4.7 Payment (:8089)

| Method | Path | Auth | Status |
|---|---|---|---|
| GET | `/api/v1/payments/orders/{orderId}` | Bearer | ✅ |
| GET | `/api/v1/payments/orders/{orderId}/escrow` | Bearer | ❌ |
| GET | `/api/v1/payments?status=&dateFrom=&page=` | Admin | ❌ |
| POST | `/api/v1/payments/escrow/{id}/confirm` | Customer,Admin | ✅ |
| POST | `/api/v1/payments/escrow/{id}/dispute` | Customer,Admin | ✅ |

### 4.8 Analytics (:8095)

| Method | Path | Auth | Code | Gateway |
|---|---|---|---|---|
| GET | `/api/v1/analytics/kpis?days=` | Admin | ✅ | ❌ |
| GET | `/api/v1/analytics/breakdown/incidents` | Admin | ✅ | ❌ |
| GET | `/api/v1/analytics/fraud/alerts` | Admin | ✅ | ❌ |
| POST | `/api/v1/analytics/fraud/alerts/{id}/acknowledge` | Admin | ❌ | ❌ |

---

## 5. Kafka — As-Built

### Producers ✅ (tất cả đều OK)

`UserRegisteredEvent`, `OrderCreatedEvent`, `OrderCancelledEvent`, `DriverRegisteredEvent`, `DriverStatusChangedEvent`, `VehicleBreakdownEvent`, `SuspiciousDriverPairDetectedEvent`, `ShipmentStartedEvent`, `BreakdownReassignmentCompletedEvent`, `DispatcherConfirmationRequiredEvent`, `PaymentCompletedEvent`

**Cần thêm (từ Driver service):**
- `DriverDocumentsSubmittedEvent` → topic `driver.documents.submitted`
- `DriverVerificationStatusUpdatedEvent` → topic `driver.verification.updated` (sau khi OCR complete)

### Consumers — Gaps

| Service | Topic | Consumer | Status |
|---|---|---|---|
| Driver | `userregistered` | `UserRegisteredConsumer` | ✅ |
| Shipment | `order.order.created` | `OrderCreatedConsumer` | ✅ |
| Shipment | `driver.driver.assigned` | `DriverAssignedConsumer` | ✅ |
| Shipment | `driver.vehicle.breakdown` | `VehicleBreakdownConsumer` | ✅ |
| Tracking | `shipment.shipment.started` | `ShipmentStartedConsumer` | ✅ |
| Tracking | `shipment.shipment.completed` | `ShipmentCompletedConsumer` | ✅ |
| Notification | all status events | 3 consumers | ✅ |
| Payment | `shipment.shipment.completed` | `OrderDeliveredConsumer` | ✅ |
| Payment | `shipment.breakdown.reassignment-completed` | `BreakdownReassignmentConsumer` | ✅ |
| Analytics | 3 topics | 3 consumers | ✅ |
| **Order** | **shipment.driver.assigned** | **`OrderAssignedConsumer`** | **❌ MISSING** |
| **Order** | **shipment.shipment.completed** | **`ShipmentCompletedConsumer`** | **❌ MISSING** |
| **Order** | **payment.payment.completed** | **`PaymentCompletedConsumer`** | **❌ MISSING** |
| **OCR Svc** | **driver.documents.submitted** | **`DriverDocumentsSubmittedConsumer`** | **❌ Service chưa tồn tại** |
| **Driver** | **ocr.driver.verification-completed** | **`DriverOcrVerificationCompletedConsumer`** | **❌ MISSING** |

---

## 6. Gateway — As-Built

| Route | Cluster | Status |
|---|---|---|
| `/api/v1/auth/*` | identity-cluster | ✅ |
| `/api/v1/orders/*` | order-cluster | ✅ |
| `/api/v1/drivers/*` | driver-cluster | ✅ |
| `/api/v1/shipments/*` | shipment-cluster | ✅ |
| `/api/v1/tracking/*` | tracking-cluster | ✅ |
| `/hubs/tracking/*` | tracking-cluster | ✅ |
| `/api/v1/notifications/*` | notification-cluster | ✅ |
| `/api/v1/payments/*` | payment-cluster | ✅ |
| `/api/v1/routes/*` | route-service-cluster | ✅ |
| `/api/v1/optimize/*` | optimizer-cluster | ✅ |
| `/api/v1/vehicles/*` | driver-cluster | **❌ MISSING** |
| `/api/v1/analytics/*` | analytics-cluster | **❌ MISSING** |
| `/api/v1/ocr/*` | ocr-cluster | **❌ MISSING** (service chưa tồn tại) |

---

## 7. Shared Infrastructure

| Component | Status | Ghi chú |
|---|---|---|
| `AggregateRoot<T>` | ✅ | |
| `Result<T>` + `Error` | ✅ | |
| `IntegrationEvent` | ✅ | |
| `KafkaEventBus` + `KafkaConsumerBase` | ✅ | |
| `RedisIdempotencyStore` | ✅ | |
| `RedisCacheService` | ✅ | |
| `MySqlConnectionFactory` | ✅ | |
| `TelemetryExtensions` | ✅ | |
| **`PagedResult<T>`** | **❌** | Cần tạo trong `Shared.Common/Primitives/` |

---

## 8. Tests & CI/CD

| Category | Count | Target |
|---|---|---|
| Unit tests | **0** | ≥70% domain coverage |
| Integration tests | **0** | Core flows |
| Contract tests | **0** | Event schemas |
| **Total** | **0** | — |

| CI/CD Component | Status |
|---|---|
| `.github/workflows/` | ❌ Empty |
| `docker/docker-compose.yml` | ✅ Exists |
| Dockerfile per service | ✅ Exists |
| Admin seed data | ❌ Missing |

---

## 9. Dispatch Automation — As-Built

| Step | Design | Code | Status |
|---|---|---|---|
| Auto-dispatch via Saga | System tự chọn driver | `DispatchSagaOrchestrator` | ✅ |
| Fallback: OR-Tools fail → greedy | Greedy assignment | Python fallback | ✅ |
| Fallback: no driver → Admin | `DispatcherReviewRequired` state | ✅ State exists | ✅ |
| Admin confirm dispatch | `POST /confirm-dispatch` | ✅ | ✅ |
| Admin decline dispatch | `POST /decline-dispatch` | ❌ Not implemented | ❌ |
| Customer notify: no driver | Notification khi Admin decline | ❌ No event/flow | ❌ |
| License grade check in Optimizer | Filter by grade+vehicle compat | ❌ Not implemented | ❌ |
