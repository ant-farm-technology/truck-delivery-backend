# Implementation Roadmap — Truck Delivery Backend

> Cập nhật: 2026-04-30 | Dựa trên khảo sát toàn bộ src/ + business context
>
> **Trạng thái:** 12 services implemented (Phase 1–7 + OCR service), 0 .NET tests / Python OCR tests ✅, 0 CI/CD
> **Services:** .NET 10 (9 services) + Rust (Route) + Python (Optimizer, **OCR — mới**)

---

## 1. Actors & Client Apps

| Actor | App | Mô tả |
|---|---|---|
| **Customer** | Flutter (monorepo với Driver + Admin) + NextJS Web | Đặt đơn, track realtime, thanh toán |
| **Driver** | Flutter (monorepo với Customer + Admin) | Nhận đơn, pickup, giao hàng |
| **Admin** | Flutter app + NextJS Admin Dashboard (monorepo) | Giám sát, fallback dispatch thủ công, quản lý hệ thống |
| **Backend** | Dự án này (.NET 10 + Rust + Python) | Auto-dispatch, routing, optimization |

### 1.1 Quy tắc dispatch (QUAN TRỌNG)

```
1. Customer tạo đơn
       ↓
2. Backend tự động: Routing → Optimization → assign driver phù hợp
       ↓ (nếu thành công)
3. Shipment InProgress → Driver nhận việc

       ↓ (nếu không tìm được driver — OR-Tools trả empty)
4. Shipment → DispatcherReviewRequired → Admin được notify
       ↓ (Admin tìm thủ công và confirm)
5. Admin confirm → Shipment InProgress

       ↓ (Admin cũng không tìm được)
6. Admin decline → Shipment Failed → Customer nhận thông báo "không có tài xế phù hợp"
```

**Admin KHÔNG chủ động điều phối** — chỉ xử lý fallback khi hệ thống bó tay.

### 1.2 Admin Account

- **Không có self-registration** — Admin account chỉ tạo qua admin module (bởi Super Admin) hoặc seeded từ đầu.
- **Seed data bắt buộc:** Tối thiểu 1 admin account trong migrations.
- Super Admin có thể tạo thêm Admin account qua `POST /api/v1/admin/accounts`.

---

## 2. Phát hiện nghiêm trọng (bổ sung từ khảo sát)

### 2.1 🚨 Order Service không có Consumer nào (Critical Bug)

`src/Services/Order/TruckDelivery.Order.Application/` **không có thư mục `Consumers/`**.

**Hậu quả:** Order status mãi là `Pending` — không bao giờ tự chuyển trạng thái dù driver đã assign và giao hàng xong.

**3 consumers phải tạo:**

| Consumer | Topic subscribe | Update Order.Status sang |
|---|---|---|
| `OrderAssignedConsumer` | `shipment.driver.assigned` | `AssignedToDriver` + lưu `ShipmentId` |
| `ShipmentCompletedConsumer` | `shipment.shipment.completed` | `Delivered` |
| `PaymentCompletedConsumer` | `payment.payment.completed` | `Completed` |

### 2.2 🚨 Domain Models thiếu fields nghiệp vụ quan trọng

**Driver aggregate** — thiếu:
- `DateOfBirth` — bắt buộc theo nghiệp vụ
- `Address` — địa chỉ tài xế
- `LicenseGrade` (enum B1/B2/C/D/E/FC/FD) — ảnh hưởng trực tiếp đến dispatch eligibility
- `LicenseExpiryDate` — cần check hạn bằng trước khi assign

**Vehicle aggregate** — thiếu:
- `LengthM`, `WidthM`, `HeightM` — kích thước xe (dùng cho bin-check) hiện không có trong aggregate mặc dù frontend-integration.md đã document
- `RegistrationNumber` (số giấy đăng ký xe)
- `RegistrationExpiryDate` (hạn đăng ký)

**User aggregate (Identity)** — thiếu:
- `PhoneNumber` — bắt buộc cho Customer (để liên lạc khi giao hàng)
- `DateOfBirth` — cần cho Customer KYC cơ bản

**RegisterRequest (Identity controller)** — thiếu `role` field → Driver không thể tự đăng ký đúng role.

### 2.3 Gateway thiếu 2 route

- `/api/v1/vehicles/*` → driver-cluster — **vehicle endpoints hoàn toàn không accessible qua Gateway**
- `/api/v1/analytics/*` → analytics-cluster — Admin Dashboard phải gọi thẳng `:8095`, bypass JWT

### 2.4 Admin "decline dispatch" chưa có

Thiếu endpoint để Admin từ chối sau khi không tìm được tài xế thủ công:
- `POST /api/v1/shipments/{id}/decline-dispatch` → Shipment `Failed` → notify Customer

---

## 3. Data Model Changes (Phải làm trước APIs)

### 3.1 LicenseGrade Enum (mới)

```csharp
// src/Services/Driver/TruckDelivery.Driver.Domain/ValueObjects/LicenseGrade.cs
public enum LicenseGrade
{
    B1 = 1,  // Xe con không kinh doanh vận tải
    B2 = 2,  // Xe con, van ≤9 chỗ kinh doanh vận tải
    C  = 3,  // Xe tải ≥3.5T (Truck3T, Truck5T, Truck10T)
    D  = 4,  // Xe khách 10–30 chỗ (van chở hàng edge case)
    E  = 5,  // Xe khách >30 chỗ (không áp dụng cho truck delivery)
    FC = 6,  // Xe đầu kéo, container (Truck15T)
    FD = 7   // Phương tiện kéo kết hợp hạng nặng
}
```

**Mapping LicenseGrade → VehicleType hợp lệ:**

| LicenseGrade | VehicleType được phép |
|---|---|
| B1 | ❌ Không eligible (không kinh doanh vận tải) |
| B2 | Motorbike, Van |
| C | Truck3T, Truck5T, Truck10T |
| D | Van (edge case — cần review nghiệp vụ) |
| E | ❌ Không applicable (xe buýt) |
| FC | Truck15T |
| FD | Truck15T |

> Optimizer service cần filter driver theo `LicenseGrade` khi chọn xe phù hợp.

### 3.1.1 DriverVerificationStatus Enum (mới)

```csharp
// src/Services/Driver/TruckDelivery.Driver.Domain/ValueObjects/DriverVerificationStatus.cs
public enum DriverVerificationStatus
{
    Draft               = 0,  // Chưa submit hồ sơ
    PendingOcrVerification = 1,  // Đã submit, đang chờ OCR async verify
    OcrVerified         = 2,  // OCR pass (confidence ≥ 0.85) — có thể set Available
    ManualReview        = 3,  // OCR flag (0.65–0.85) — Admin xem xét thủ công
    AdminVerified       = 4,  // Admin xác nhận thủ công — có thể set Available
    Rejected            = 5   // Từ chối (OCR < 0.65 hoặc Admin reject) — phải upload lại
}
```

**Guard — Driver.UpdateStatus():**
```csharp
// Driver chỉ được set Available khi VerificationStatus = OcrVerified | AdminVerified
if (newStatus == DriverStatus.Available &&
    VerificationStatus != DriverVerificationStatus.OcrVerified &&
    VerificationStatus != DriverVerificationStatus.AdminVerified)
{
    return Result.Failure(Error.Validation("Driver.Verification",
        "Tài xế chưa được xác minh hồ sơ."));
}
```

### 3.2 Driver Aggregate — Fields cần thêm

```csharp
// Thêm vào src/Services/Driver/TruckDelivery.Driver.Domain/Aggregates/Driver.cs

// --- Personal info ---
public DateOnly DateOfBirth { get; private set; }
public string Address { get; private set; } = default!;
public string IdCardNumber { get; private set; } = default!;   // Số CCCD — unique

// --- License info ---
public LicenseGrade LicenseGrade { get; private set; }
public DateOnly LicenseExpiryDate { get; private set; }

// --- Document photos (7 ảnh bắt buộc) ---
public string PortraitPhotoUrl { get; private set; } = default!;
public string IdCardFrontUrl { get; private set; } = default!;
public string IdCardBackUrl { get; private set; } = default!;
public string LicenseFrontUrl { get; private set; } = default!;
public string LicenseBackUrl { get; private set; } = default!;
public string VehicleRegFrontUrl { get; private set; } = default!;
public string VehicleRegBackUrl { get; private set; } = default!;

// --- Verification ---
public DriverVerificationStatus VerificationStatus { get; private set; }
public float? OcrConfidenceScore { get; private set; }
public string? VerificationNotes { get; private set; }
```

**Factory method cập nhật:**
```csharp
public static Result<Driver> Create(
    Guid userId,
    string email,
    string firstName,
    string lastName,
    string phoneNumber,
    string licenseNumber,
    LicenseGrade licenseGrade,    // MỚI
    DateOnly licenseExpiryDate,   // MỚI
    DateOnly dateOfBirth,         // MỚI
    string address)               // MỚI
{
    if (licenseGrade == LicenseGrade.B1 || licenseGrade == LicenseGrade.E)
        return Result.Failure<Driver>(Error.Validation("Driver.LicenseGrade",
            "Hạng bằng này không đủ điều kiện vận tải hàng hóa."));
    
    if (licenseExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow))
        return Result.Failure<Driver>(Error.Validation("Driver.LicenseExpiryDate",
            "Bằng lái đã hết hạn."));
    // ...
}
```

**DB migration:** thêm columns vào bảng `drivers`:
- `DateOfBirth` DATE NOT NULL
- `Address` VARCHAR(500) NOT NULL
- `IdCardNumber` VARCHAR(20) NOT NULL UNIQUE
- `LicenseGrade` TINYINT NOT NULL
- `LicenseExpiryDate` DATE NOT NULL
- `PortraitPhotoUrl` VARCHAR(1000) NULL
- `IdCardFrontUrl` VARCHAR(1000) NULL
- `IdCardBackUrl` VARCHAR(1000) NULL
- `LicenseFrontUrl` VARCHAR(1000) NULL
- `LicenseBackUrl` VARCHAR(1000) NULL
- `VehicleRegFrontUrl` VARCHAR(1000) NULL
- `VehicleRegBackUrl` VARCHAR(1000) NULL
- `VerificationStatus` TINYINT NOT NULL DEFAULT 0
- `OcrConfidenceScore` FLOAT NULL
- `VerificationNotes` VARCHAR(1000) NULL

### 3.3 Vehicle Aggregate — Fields cần thêm

```csharp
// Thêm vào src/Services/Driver/TruckDelivery.Driver.Domain/Aggregates/Vehicle.cs
public decimal LengthM { get; private set; }              // Chiều dài khoang hàng
public decimal WidthM { get; private set; }               // Chiều rộng khoang hàng
public decimal HeightM { get; private set; }              // Chiều cao khoang hàng
public string RegistrationNumber { get; private set; } = default!;  // Số đăng ký xe
public DateOnly RegistrationExpiryDate { get; private set; }        // Hạn đăng kiểm
```

**DB migration:** thêm columns vào bảng `vehicles`.

### 3.4 User Aggregate (Identity) — Fields cần thêm

```csharp
// Thêm vào src/Services/Identity/TruckDelivery.Identity.Domain/Aggregates/User.cs
public string? PhoneNumber { get; private set; }          // Số điện thoại
public DateOnly? DateOfBirth { get; private set; }        // Ngày sinh
```

**Factory method cập nhật:**
```csharp
public static Result<User> Create(
    string email,
    string password,
    string firstName,
    string lastName,
    UserRole role,
    string? phoneNumber = null,
    DateOnly? dateOfBirth = null)
```

**DB migration:** thêm columns `PhoneNumber`, `DateOfBirth` vào bảng `users`.

---

## 4. Registration Flows

### 4.1 Customer Registration

**Thông tin bắt buộc:**

| Field | Validation |
|---|---|
| Email | unique, valid format |
| Password | ≥8 ký tự, có chữ hoa/số |
| FirstName, LastName | not empty |
| PhoneNumber | format Vietnam (+84 / 0...) |
| DateOfBirth | optional, must be ≥18 tuổi |

**Endpoint:** `POST /api/v1/auth/register` (Anonymous)

**Sửa `RegisterRequest`:**
```csharp
// src/Services/Identity/TruckDelivery.Identity.Api/Controllers/AuthController.cs
public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PhoneNumber,          // MỚI — bắt buộc
    DateOnly? DateOfBirth = null); // MỚI — optional
// KHÔNG có Role field — Customer role được set cứng trong handler
```

**Handler** `RegisterUserCommandHandler` → hardcode `UserRole.Customer` → publish `UserRegisteredEvent`.

### 4.2 Driver Registration (Self-service, 3 bước)

**Bước 1 — Tạo User account (Anonymous):**

```http
POST /api/v1/auth/register/driver
{
  "email": "driver@example.com",
  "password": "P@ssw0rd",
  "firstName": "Nguyễn",
  "lastName": "Văn A",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1990-05-15"
}
→ { userId }
```

Handler: tạo `User` với `role = Driver`, publish `UserRegisteredEvent(role=Driver)`.

**Bước 2 — Upload ảnh + OCR auto-fill (sau khi login):**

```http
# Lấy pre-signed URLs upload 7 ảnh lên S3
GET /api/v1/uploads/presigned-url?type=driver-document&count=7
→ { urls: [{ field, upload_url, final_url }] }

# Driver app upload 7 ảnh lên S3 trực tiếp

# OCR auto-fill từng loại giấy tờ
POST /api/v1/ocr/extract/id-card     { front_url, back_url }
→ { id_number, full_name, date_of_birth, address, suggested_form_values }

POST /api/v1/ocr/extract/license     { front_url, back_url }
→ { license_number, license_grade, expiry_date, suggested_form_values }

POST /api/v1/ocr/extract/vehicle-reg { front_url, back_url }
→ { license_plate, brand, model, registration_number, suggested_form_values }

# Client pre-fills form với suggested_form_values, driver review/chỉnh sửa
```

**Bước 3 — Submit hồ sơ đầy đủ (sau khi review OCR data):**

```http
POST /api/v1/drivers/register
Authorization: Bearer <token>   (role = Driver)
{
  "idCardNumber": "079123456789",
  "address": "123 Nguyễn Trãi, Q.1, TP.HCM",
  "licenseNumber": "079123456789",
  "licenseGrade": "C",
  "licenseExpiryDate": "2028-12-31",
  "photos": {
    "portraitUrl": "https://s3.../portrait.jpg",
    "idCardFrontUrl": "https://s3.../cccd_front.jpg",
    "idCardBackUrl": "https://s3.../cccd_back.jpg",
    "licenseFrontUrl": "https://s3.../gplx_front.jpg",
    "licenseBackUrl": "https://s3.../gplx_back.jpg",
    "vehicleRegFrontUrl": "https://s3.../dangky_front.jpg",
    "vehicleRegBackUrl": "https://s3.../dangky_back.jpg"
  },
  "vehicle": {
    "licensePlate": "51A-12345",
    "brand": "Isuzu",
    "model": "QKR77",
    "type": "Truck3T",
    "maxWeightKg": 3000,
    "maxVolumeCbm": 12.5,
    "lengthM": 4.2,
    "widthM": 1.8,
    "heightM": 1.8,
    "yearOfManufacture": 2020,
    "registrationNumber": "11AA-123456",
    "registrationExpiryDate": "2026-06-30"
  }
}
→ { driverId, verificationStatus: "PendingOcrVerification" }
```

Handler: Tạo `Driver` (VerificationStatus=PendingOcrVerification) + `Vehicle` trong cùng transaction → publish `DriverDocumentsSubmittedEvent` → Kafka → OCR service async verify.

**Validation rules (đồng bộ tại handler):**
- `licenseGrade` không được là B1 hoặc E
- `licenseExpiryDate` phải > today
- `registrationExpiryDate` phải > today
- Vehicle type phải compatible với licenseGrade (xem mapping 3.1)
- `idCardNumber` phải unique (CCCD không được dùng 2 lần)
- Tất cả 7 photo URLs phải hợp lệ (non-empty, S3 format)

**Sau khi OCR async verify:**
- `OcrVerified` (confidence ≥ 0.85) → driver có thể set Available
- `ManualReview` → Admin được notify, driver chờ
- `Rejected` → driver nhận thông báo lý do, phải upload lại

**Files cần tạo:**
```
src/Services/Driver/TruckDelivery.Driver.Application/
  Commands/
    SelfRegisterDriver/
      SelfRegisterDriverCommand.cs
      SelfRegisterDriverCommandHandler.cs
      SelfRegisterDriverCommandValidator.cs

src/Services/Identity/TruckDelivery.Identity.Api/Controllers/
  → Thêm endpoint POST /auth/register/driver vào AuthController
  → Hoặc tạo DriversRegistrationController.cs (nếu muốn tách)
```

### 4.3 Admin Account (No self-registration)

**Tạo qua Admin Module (Super Admin):**
```http
POST /api/v1/admin/accounts
Authorization: Bearer <super-admin-token>
{
  "email": "admin@truckdelivery.vn",
  "password": "GeneratedP@ssw0rd",
  "firstName": "Admin",
  "lastName": "System"
}
```

**Seed data bắt buộc:**

```csharp
// src/Services/Identity/TruckDelivery.Identity.Infrastructure/Persistence/Seeds/AdminSeeder.cs
public static class AdminSeeder
{
    public static async Task SeedAsync(IdentityDbContext context)
    {
        if (await context.Users.AnyAsync(u => u.Role == UserRole.Admin)) return;

        var admin = User.Create(
            "admin@truckdelivery.vn",
            "Admin@123456",            // đổi ngay sau khi deploy
            "System",
            "Admin",
            UserRole.Admin,
            phoneNumber: "+84901000001").Value;

        context.Users.Add(admin);
        await context.SaveChangesAsync();
    }
}
```

**Gọi trong `Program.cs`:** `await AdminSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<IdentityDbContext>())`

---

## 5. Dispatch Flow Enhancement

### 5.1 Admin Decline Dispatch (Gap mới)

Khi Admin xác nhận không tìm được tài xế:

```http
POST /api/v1/shipments/{id}/decline-dispatch
Authorization: Bearer <admin-token>
{
  "reason": "Không có tài xế phù hợp trong khu vực"
}
→ 204 No Content
```

**Handler flow:**
1. Shipment → `Failed`
2. Publish `ShipmentFailedEvent` → Kafka
3. Order service consume → Order → `Cancelled` (với reason "Không có tài xế phù hợp")
4. Notification service consume → push/SMS cho Customer

**Files cần tạo:**
```
src/Services/Shipment/TruckDelivery.Shipment.Application/
  Commands/
    DeclineDispatch/
      DeclineDispatchCommand.cs
      DeclineDispatchCommandHandler.cs
```

### 5.2 License Grade Check trong Optimizer

**Hiện tại:** Optimizer nhận list available drivers không có license grade info.

**Cần thêm vào `DriverAssignmentRequestedEvent`** (hoặc Driver query):
- `driver.licenseGrade` → Optimizer filter chỉ assign driver có grade compatible với vehicle type của shipment.

**Sửa `DriverAssignmentRequestedEvent`:**
```csharp
// Thêm field:
public string RequiredLicenseGrade { get; init; } // "B2" | "C" | "FC" | "FD"
```

**Sửa Optimizer (Python):** filter `available_drivers` theo `license_grade_compatible_with_vehicle_type`.

---

## 6. Phase 1 — Critical Fixes (Tuần 1–2)

### 6.1 Data model changes (Ưu tiên đầu — 1.5 ngày)

| Task | Files | Effort |
|---|---|---|
| Add `LicenseGrade` enum | `Driver.Domain/ValueObjects/LicenseGrade.cs` (tạo mới) | XS |
| Update `Driver` aggregate + migration | `Driver.cs`, `DriversConfiguration.cs`, migration | M |
| Update `Vehicle` aggregate (dimensions + registration) + migration | `Vehicle.cs`, `VehiclesConfiguration.cs`, migration | M |
| Update `User` aggregate (phone + DOB) + migration | `User.cs`, `UserConfiguration.cs`, migration | S |

### 6.2 Order Service Consumers (Critical — 1 ngày)

| Task | Files | Effort |
|---|---|---|
| `OrderAssignedConsumer` (+ lưu ShipmentId vào Order) | `Consumers/`, migration | L |
| `ShipmentCompletedConsumer` | `Consumers/` | S |
| `PaymentCompletedConsumer` | `Consumers/` | S |
| `ShipmentId` field trong `OrderDto` | `OrderDto.cs` | XS |

### 6.3 Gateway routes (30 phút)

```json
// Thêm vào appsettings.json:
"vehicle-route":    { "ClusterId": "driver-cluster",    "Match": { "Path": "/api/v1/vehicles/{**catch-all}" } },
"analytics-route":  { "ClusterId": "analytics-cluster", "Match": { "Path": "/api/v1/analytics/{**catch-all}" } },
"ocr-route":        { "ClusterId": "ocr-cluster",       "Match": { "Path": "/api/v1/ocr/{**catch-all}" } }

// Thêm vào Clusters:
"ocr-cluster": { "Destinations": { "primary": { "Address": "http://ocr-service:8090/" } } }
```

### 6.4 Admin Seed Data (2h)

| Task | Files | Effort |
|---|---|---|
| `AdminSeeder.cs` | Identity.Infrastructure/Persistence/Seeds/ | S |
| Wire seeder trong `Program.cs` | Identity.Api/Program.cs | XS |

### 6.5 List Shipments endpoint (4h)

```
GET /api/v1/shipments?status=&customerId=&driverId=&page=&pageSize=
```

Giải quyết: A1 (Admin list), C7 (Customer list), D1 (Driver active shipment).

### 6.6 Escrow lookup + Analytics Gateway (2h)

```
GET /api/v1/payments/orders/{orderId}/escrow
```

### Phase 1 Checklist

| # | Task | Effort | Ảnh hưởng |
|---|---|---|---|
| 1 | Data model: Driver + Vehicle + User | M+M+S | Foundation |
| 2 | LicenseGrade + DriverVerificationStatus enums | XS | Foundation |
| 3 | Order consumers (3 consumers) + ShipmentId | L | Critical bug |
| 4 | Admin seed data | S | Admin can login |
| 5 | Gateway routes (vehicles + analytics + **ocr**) | XS | ✅ Done |
| 6 | List Shipments | M | A1, C7, D1 |
| 7 | Escrow lookup | S | C2 |
| 8 | Admin decline-dispatch endpoint | S | Dispatch flow |
| 9 | Pre-signed URL endpoint (`GET /api/v1/uploads/presigned-url`) | S | Driver photo upload |

> **Kết quả Phase 1:** System functional end-to-end. Admin có thể login. Order status tự cập nhật. Driver có thể upload ảnh để đăng ký.

---

## 7. Phase 2 — Registration & UX (Tuần 2–4)

### 7.1 Customer Registration (update existing — 2h)

- Thêm `PhoneNumber` vào `RegisterRequest` + `RegisterUserCommand` + `User.Create()`
- Thêm `DateOfBirth` (optional)
- Update `UserRegisteredEvent` để carry `phoneNumber`

### 7.2 Driver Registration (self-service — 2 ngày)

| Task | Files | Effort |
|---|---|---|
| `POST /api/v1/auth/register/driver` endpoint | `AuthController.cs` | S |
| `SelfRegisterDriverCommand` + Handler + Validator | `Commands/SelfRegisterDriver/` | M |
| Validate licenseGrade vs vehicleType compatibility | domain guard | S |
| Validation: license not expired, registration not expired | validator | S |
| `DriverVerificationStatus` guard trong `Driver.UpdateStatus()` | `Driver.cs` | XS |

### 7.2.1 Admin Driver Verification (1 ngày)

| Task | Files | Effort |
|---|---|---|
| `GET /api/v1/drivers/pending-verification` — ManualReview queue | `DriversController.cs` | M |
| `POST /api/v1/drivers/{id}/verify` — Admin verify sau ManualReview | `Commands/AdminVerifyDriver/` | S |
| `POST /api/v1/drivers/{id}/reject-verification` — Admin reject, yêu cầu upload lại | `Commands/AdminRejectDriver/` | S |
| `DriverOcrVerificationCompletedConsumer` trong Driver service | `Consumers/` | M |

### 7.3 Admin account management (1 ngày)

| Task | Files | Effort |
|---|---|---|
| `POST /api/v1/admin/accounts` (Super Admin only) | `AdminController.cs` | M |
| `CreateAdminCommand` + Handler | `Commands/CreateAdmin/` | S |
| `UserRole.SuperAdmin` (nếu cần phân cấp) | `UserRole.cs` | XS |

### 7.4 Pagination cho tất cả list endpoints (1 ngày)

Tạo `PagedResult<T>` trong `TruckDelivery.Shared.Common/Primitives/`:
```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
}
```

Cập nhật:
- `ListOrdersByCustomerQueryHandler` — thêm `COUNT(*)`, filter `status`/`dateFrom`/`dateTo`
- `ListDriversQueryHandler` (mới) — filter `status`
- `ListVehiclesQueryHandler` (mới) — filter `status`/`driverId`/`type`
- `ListPaymentsQueryHandler` (mới)

### 7.5 Các endpoints còn lại (M)

| Endpoint | Service | Gap |
|---|---|---|
| `GET /api/v1/drivers?status=&page=` | Driver | A3 |
| `GET /api/v1/vehicles?status=&page=` | Driver | A4 |
| `GET /api/v1/payments?status=&dateFrom=&page=` | Payment | A2 |
| `PUT /api/v1/vehicles/{id}/status` | Driver | A8 |
| `POST /api/v1/analytics/fraud/alerts/{id}/acknowledge` | Analytics | A7 |

### 7.6 Security fixes (XS — làm ngay)

```csharp
// DriversController.cs:
[HttpPut("{id:guid}/status")]
[Authorize(Roles = "Admin,Driver")]  // THÊM role guard

// UpdateShipmentStatusCommandHandler: Driver chỉ set PickedUp|InTransit|Delivered
// DriverDto: thêm TrustScore field
```

### 7.7 FCM Device Token + SignalR DriverAssigned (M)

```
POST /api/v1/notifications/device-tokens
```
+ `DeviceToken` aggregate + `DriverAssigned` SignalR event từ `ShipmentStartedConsumer`.

---

## 8. Phase 3 — Testing (Tuần 4–8)

> **Hiện trạng: 0 tests.** Xem chi tiết tại `docs/architecture-business/09-implementation-status/02-testing-plan.md`.

**Ưu tiên:**
1. Unit tests cho domain: `Order`, `Driver` (LicenseGrade validation, TrustScore), `Shipment`, `Payment`
2. Integration tests: Order consumers (idempotency), Dispatch saga
3. Contract tests: event schemas

**Test projects (thêm vào `TruckDelivery.slnx` — không tạo `.sln` mới):**
```
tests/
  Unit/
    TruckDelivery.Order.Domain.Tests/
    TruckDelivery.Driver.Domain.Tests/
    TruckDelivery.Shipment.Domain.Tests/
    TruckDelivery.Payment.Domain.Tests/
  Integration/
    TruckDelivery.Order.Integration.Tests/
    TruckDelivery.Shipment.Integration.Tests/
  Contract/
    TruckDelivery.EventContracts.Tests/
```

---

## 9. Phase 4 — CI/CD + Production Readiness (Tuần 6–10)

### 9.1 GitHub Actions

```
.github/workflows/
  build-test.yml        ← build + unit tests (PR gate, < 2 phút)
  integration.yml       ← integration tests (on merge to develop)
  docker-publish.yml    ← build + push images
```

### 9.2 Docker Compose validation

`docker/docker-compose.yml` đã tồn tại — cần verify:
- Tất cả 11 services + analytics service có entry
- Health check `depends_on: condition: service_healthy`
- Volume persistence cho MySQL, MongoDB, PostGIS

### 9.3 Photo Upload cho Breakdown (Gap D5 — M)

```
GET /api/v1/uploads/presigned-url?type=breakdown-photo
→ { uploadUrl, expiresIn }
```

Pre-signed S3/MinIO URL — Driver upload trực tiếp, paste URL vào `report-breakdown`.

### 9.4 Rate limit GPS per-user (Gap X4 — S)

`/api/v1/tracking/location`: rate limit theo JWT `sub` (không theo IP).

---

## 10. Dependency Graph

```
[Domain Models] ──────────────────────┐
  LicenseGrade enum                   │ TRƯỚC HẾT
  Driver fields (DOB, grade, addr)    │
  Vehicle fields (dims, registration) │
  User fields (phone, DOB)            │
                                      ▼
[Admin Seed Data] ─────────────── [Order Consumers]
  AdminSeeder.cs                   OrderAssigned
  Program.cs wire                  ShipmentCompleted      PHASE 1 CORE
  Admin can login ✅               PaymentCompleted
                                   Order status works ✅
                    ┌──────────────────────────────────────┐
                    ▼                                      ▼
[Gateway Fixes]             [List Endpoints]
  vehicles route               List Shipments
  analytics route              (+ active driver query)
  30 phút ✅                   Escrow lookup

══════════════════════════ PHASE 2 ══════════════════════════
[Registration Flows]         [Pagination]         [Security]
  Customer (phone/DOB)         PagedResult<T>       Role guards
  Driver self-register         All list handlers    Driver status
  Admin management             (Orders, Drivers,    Shipment restrict
                               Vehicles, Payments)

══════════════════════════ PHASE 3 ══════════════════════════
[Tests] — sau Phase 1 stable
  Unit → Integration → Contract

══════════════════════════ PHASE 4 ══════════════════════════
[CI/CD] — song song Phase 3
  GitHub Actions → Docker publish
```

---

## 11. Effort Summary

| Phase | Nội dung | Effort | Timeline |
|---|---|---|---|
| Phase 1 | Data models + Consumers + Critical fixes | ~4 ngày | Tuần 1 |
| Phase 2 | Registration flows + List APIs + UX | ~6 ngày | Tuần 2–3 |
| Phase 3 | Testing (từ 0) | ~10 ngày | Tuần 4–7 |
| Phase 4 | CI/CD + Production | ~3 ngày | Song song 6–8 |
| **Tổng** | | **~23 ngày dev** | **~8 tuần** |

---

## 12. Quick Wins (< 1 giờ mỗi cái — làm ngay)

```
1. Gateway: thêm vehicle-route + analytics-route vào appsettings.json
2. DriversController: thêm [Authorize(Roles = "Admin,Driver")] vào PUT /status
3. DriverDto: thêm TrustScore field
4. PUT /api/v1/vehicles/{id}/status endpoint
5. POST /api/v1/analytics/fraud/alerts/{id}/acknowledge endpoint
6. AdminSeeder.cs + wire vào Program.cs
```

---

## 13. Phụ lục — API Endpoints Đầy đủ (Target State)

### Customer App

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Anonymous | Done (cần thêm phone) |
| POST | `/api/v1/auth/login` | Anonymous | Done |
| POST | `/api/v1/auth/refresh` | Anonymous | Done |
| POST | `/api/v1/orders` | Customer | Done |
| GET | `/api/v1/orders/{id}` | Bearer | Done (cần ShipmentId) |
| GET | `/api/v1/orders?status=&page=` | Bearer | P2 (pagination) |
| DELETE | `/api/v1/orders/{id}` | Customer | Done |
| GET | `/api/v1/shipments/{id}` | Bearer | Done |
| GET | `/api/v1/shipments?customerId=&page=` | Bearer | P1 |
| GET | `/api/v1/tracking/shipments/{id}/points` | Bearer | Done |
| GET | `/api/v1/payments/orders/{orderId}` | Bearer | Done |
| GET | `/api/v1/payments/orders/{orderId}/escrow` | Bearer | P1 |
| WS | `/hubs/tracking` → JoinShipmentGroup | Bearer | Done |
| POST | `/api/v1/notifications/device-tokens` | Bearer | P2 |

### Driver App

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| POST | `/api/v1/auth/register/driver` | Anonymous | P2 |
| POST | `/api/v1/drivers/register` | Driver | P2 |
| GET | `/api/v1/drivers/{id}` | Bearer | Done (cần TrustScore) |
| PUT | `/api/v1/drivers/{id}/status` | Admin,Driver | Done (fix role P1) |
| POST | `/api/v1/tracking/location` | Driver | Done |
| PUT | `/api/v1/shipments/{id}/status` | Driver | Done (fix restrict P2) |
| GET | `/api/v1/shipments/active?driverId=` | Driver | P1 |
| GET | `/api/v1/shipments?driverId=&status=Completed&page=` | Driver | P1 |
| POST | `/api/v1/drivers/{id}/report-breakdown` | Driver | Done |
| GET | `/api/v1/uploads/presigned-url` | Driver | P1 |
| POST | `/api/v1/ocr/extract/id-card` | Driver | OCR Svc |
| POST | `/api/v1/ocr/extract/license` | Driver | OCR Svc |
| POST | `/api/v1/ocr/extract/vehicle-reg` | Driver | OCR Svc |
| WS | `/hubs/tracking` → JoinDriverGroup | Bearer | Done |
| POST | `/api/v1/notifications/device-tokens` | Bearer | P2 |

### Admin Portal

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| POST | `/api/v1/admin/accounts` | SuperAdmin | P2 |
| POST | `/api/v1/drivers` | Admin | Done |
| GET | `/api/v1/drivers?status=&page=` | Admin | P2 |
| GET | `/api/v1/drivers/{id}` | Bearer | Done |
| GET | `/api/v1/drivers/pending-verification` | Admin | P2 |
| POST | `/api/v1/drivers/{id}/verify` | Admin | P2 |
| POST | `/api/v1/drivers/{id}/reject-verification` | Admin | P2 |
| POST | `/api/v1/vehicles` | Admin | Done |
| GET | `/api/v1/vehicles?status=&page=` | Admin | P2 |
| GET | `/api/v1/vehicles/{id}` | Bearer | Done |
| PUT | `/api/v1/vehicles/{id}/status` | Admin | P2 |
| POST | `/api/v1/drivers/{id}/assign-vehicle` | Admin | Done |
| GET | `/api/v1/shipments?status=&page=` | Admin | P1 |
| POST | `/api/v1/shipments/{id}/confirm-dispatch` | Admin | Done |
| POST | `/api/v1/shipments/{id}/decline-dispatch` | Admin | P1 |
| GET | `/api/v1/payments?status=&dateFrom=&page=` | Admin | P2 |
| GET | `/api/v1/analytics/kpis?days=` | Admin | Done (fix gateway P1) |
| GET | `/api/v1/analytics/breakdown/incidents` | Admin | Done (fix gateway P1) |
| GET | `/api/v1/analytics/fraud/alerts` | Admin | Done (fix gateway P1) |
| POST | `/api/v1/analytics/fraud/alerts/{id}/acknowledge` | Admin | P2 |
| WS | `/hubs/tracking` → JoinAdminGroup | Admin | Done |

### OCR Service (Internal / Driver-facing) ✅ Implemented

| Method | Endpoint | Auth | Ghi chú |
|---|---|---|---|
| POST | `/api/v1/ocr/extract/id-card` | Driver | ✅ Phase A auto-fill — sync |
| POST | `/api/v1/ocr/extract/license` | Driver | ✅ Phase A auto-fill — sync |
| POST | `/api/v1/ocr/extract/vehicle-reg` | Driver | ✅ Phase A auto-fill — sync |
| GET | `/health` | Anonymous | ✅ Liveness |
| GET | `/ready` | Anonymous | ✅ Readiness |
| GET | `/metrics` | Anonymous | ✅ Prometheus |
