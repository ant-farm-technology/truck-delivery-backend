# Đề Xuất Nâng Cấp & Bổ Sung — Truck Delivery Backend

> **Ngày:** 2026-05-01 | **Tác giả:** QuanDH + Claude Sonnet 4.6  
> **Phạm vi:** Tổng hợp từ toàn bộ `docs/`, code hiện tại, CLAUDE.md, và kết quả review 2026-04-30  
> **Ưu tiên:** 🔴 Critical (block launch) → 🟡 High → 🟢 Medium → ⚪ Nice-to-have  
> **Sprint 1 hoàn thành:** 2026-05-01 — 8/8 items ✅

---

## 0. Tóm Tắt Điều Hành

Sau khi khảo sát toàn bộ 12 microservices, documentation, test coverage, và CI/CD:

| Hạng mục | Hiện trạng |
|---|---|
| Backend code | ✅ Hoàn thành Phase 1–7 + bug fixes + Sprint 1 |
| Unit tests | ✅ 4 test projects tạo; CI build gate active |
| Integration tests | ❌ 0 tests |
| Contract tests | ❌ 0 tests |
| CI/CD | ✅ `build-test.yml` + `docker-publish.yml` — thiếu `integration.yml` |
| Notification | ❌ Stubs — không gửi thực |
| Payment | ❌ Chỉ COD — không có VNPay/card |
| Security | ✅ 4 security gaps đã vá (Sprint 1) |
| Documentation | ⚠️ Nhiều file lỗi thời; thiếu Admin guide, schema diagram |

**Tổng số vấn đề: 28 items — 8 Sprint 1 đã xong, 20 còn lại.**

---

## 1. Nghiệp Vụ (Business)

### 1.1 🔴 LicenseGrade ↔ VehicleType chưa được enforce trong Optimizer

**Vấn đề:** Optimizer Python (`POST /optimize`) nhận danh sách driver khả dụng nhưng **không filter theo license grade**. Hậu quả: hệ thống có thể gán driver B2 cho Truck15T — vi phạm luật giao thông và vô hiệu hóa bảo hiểm hàng hóa.

**Mapping cần enforce:**

| LicenseGrade | VehicleType được phép |
|---|---|
| B2 | Motorbike, Van |
| C | Truck3T, Truck5T, Truck10T |
| FC / FD | Truck15T |
| B1, E | ❌ Không cho phép vận tải thương mại |

**Đề xuất:**
- Thêm `required_license_grades: list[str]` vào `OptimizeRequest` (Shipment service tính toán từ VehicleType của shipment, truyền vào Optimizer).
- Optimizer filter `available_drivers` theo `license_grade in required_license_grades` trước khi chạy VRP.
- Cập nhật `DriverAssignmentRequestedEvent` thêm field `RequiredLicenseGrades: string[]`.

**Files cần sửa:**
```
src/Services/Optimizer/truck-delivery-optimizer/app/models.py
src/Services/Optimizer/truck-delivery-optimizer/app/solver.py
src/Services/Shipment/TruckDelivery.Shipment.Application/IntegrationEvents/DriverAssignmentRequestedEvent.cs
```

---

### 1.2 🔴 Notification Senders là Stubs — Push/SMS/Email không gửi thực

**Vấn đề:** `StubPushSender`, `StubSmsSender`, `StubEmailSender` chỉ ghi log. Toàn bộ luồng notification (driver được assign, đơn hoàn thành, thanh toán) **không tới tay người dùng**.

**Đề xuất — implement theo thứ tự ưu tiên:**

**Bước 1 — Firebase FCM (Push notification — cao nhất):**
```csharp
// src/Services/Notification/TruckDelivery.Notification.Infrastructure/Notifications/FcmPushSender.cs
// Dùng FirebaseAdmin SDK (google.golang.org/firebase/admin → .NET: FirebaseAdmin NuGet)
// Input: DeviceToken (từ bảng device_tokens), payload JSON
// Endpoint: FCM v1 API (https://fcm.googleapis.com/v1/projects/{projectId}/messages:send)
```

**Bước 2 — Twilio SMS:**
```csharp
// src/Services/Notification/TruckDelivery.Notification.Infrastructure/Notifications/TwilioSmsSender.cs
// Twilio.AspNet.Core NuGet package
// Config: AccountSid, AuthToken, FromNumber
```

**Bước 3 — SMTP Email:**
```csharp
// src/Services/Notification/TruckDelivery.Notification.Infrastructure/Notifications/SmtpEmailSender.cs
// MailKit NuGet — hỗ trợ OAuth2/Gmail SMTP
```

**Config cần thêm vào `appsettings.json`:**
```json
{
  "Firebase": { "ProjectId": "", "CredentialsJson": "" },
  "Twilio":   { "AccountSid": "", "AuthToken": "", "FromNumber": "" },
  "Smtp":     { "Host": "", "Port": 587, "Username": "", "Password": "" }
}
```

---

### 1.3 🔴 Payment Gateway chưa tích hợp — Chỉ có COD

**Vấn đề:** `OrderDeliveredConsumer` → `CreatePaymentCommand` → auto-complete ngay. Không có luồng thanh toán thực (card/VNPay/MoMo).

**Đề xuất — VNPay integration (phù hợp thị trường VN):**

```
Luồng VNPay:
Customer chọn "Thanh toán VNPay" khi tạo Order
  → Payment Service tạo Payment (Pending)
  → Tạo VNPay payment URL (HMAC-SHA512 signature)
  → Return URL về client
  → Client redirect sang VNPay
  → VNPay callback POST /api/v1/payments/webhook/vnpay
  → Verify checksum → Payment → Authorized
  → COD: auto-Complete sau Delivered
```

**Files cần tạo:**
```
src/Services/Payment/TruckDelivery.Payment.Infrastructure/
  Gateways/
    IPaymentGateway.cs
    VnPayGateway.cs           ← HMAC-SHA512, tạo URL + verify callback
    CodGateway.cs             ← Auto-complete existing logic
src/Services/Payment/TruckDelivery.Payment.Api/
  Controllers/WebhookController.cs  ← POST /api/v1/payments/webhook/vnpay
```

**Lưu ý:** `PaymentMethod` enum cần thêm vào Order aggregate: `COD=1, VNPay=2, Card=3`.

---

### 1.4 🟡 Admin Notification khi Driver vào ManualReview

**Vấn đề:** Khi OCR trả về confidence 0.65–0.85 → Driver vào `ManualReview` → **Admin không được notify**. Admin phải chủ động poll `GET /api/v1/drivers/pending-verification`.

**Đề xuất:**
- Publish `DriverManualReviewRequiredEvent` từ Driver service khi ApplyOcrResult → ManualReview.
- Notification service consume → gửi email + push notification cho Admin group.

**Files cần tạo:**
```
src/Services/Driver/TruckDelivery.Driver.Application/
  IntegrationEvents/DriverManualReviewRequiredEvent.cs
src/Services/Notification/TruckDelivery.Notification.Application/
  Consumers/DriverManualReviewConsumer.cs
```

---

### 1.5 🟡 SignalR — Thiếu `DriverAssigned` Event từ Tracking Hub

**Vấn đề:** Khi Shipment chuyển sang InProgress, Tracking service nhận `ShipmentStartedEvent` và bắt đầu session, nhưng **không emit SignalR event** về cho Driver app. Driver không biết mình được giao việc trừ khi dùng push notification stub (không hoạt động).

**Đề xuất:**
- Trong `ShipmentStartedConsumer` của Tracking service, sau khi StartTracking, gọi `ITrackingHub.Clients.Group($"driver:{driverId}").SendAsync("DriverAssigned", payload)`.
- Payload: `{ shipmentId, orderId, pickupAddress, deliveryAddress, customerName, customerPhone }`.

**Files cần sửa:**
```
src/Services/Tracking/TruckDelivery.Tracking.Application/
  Consumers/ShipmentStartedConsumer.cs   ← thêm SignalR emit
src/Services/Tracking/TruckDelivery.Tracking.Api/
  Hubs/TrackingHub.cs                    ← thêm method DriverAssigned nếu cần
```

---

### 1.6 🟡 Customer không biết `shipmentId` để theo dõi real-time

**Vấn đề:** Customer tạo order → nhận `orderId`. Để join SignalR group `shipment:{shipmentId}` phải biết `shipmentId`. Hiện `OrderDto` có field `ShipmentId: Guid?` (đã thêm vào aggregate) nhưng cần verify `OrderDto` trong query handler đã include field này.

**Đề xuất:**
- Verify `ListOrdersByCustomerQueryHandler` và `GetOrderByIdQueryHandler` trả về `ShipmentId` trong DTO.
- Nếu chưa: thêm `shipment_id` vào Dapper query `SELECT ... o.ShipmentId ...`.

---

### 1.7 🟡 `POST /api/v1/admin/accounts` — Admin tự tạo Admin (Super Admin)

**Vấn đề:** Chưa có endpoint để Super Admin tạo thêm Admin account.

**Đề xuất:**
```csharp
// src/Services/Identity/TruckDelivery.Identity.Api/Controllers/AdminController.cs
[HttpPost("api/v1/admin/accounts")]
[Authorize(Roles = "Admin")]  // chỉ Admin hiện tại mới tạo được Admin mới
public async Task<IActionResult> CreateAdminAccount(CreateAdminRequest request, CancellationToken ct)
{
    var command = new RegisterUserCommand(request.Email, request.Password,
        request.FirstName, request.LastName, UserRole.Admin);
    var userId = await _mediator.Send(command, ct);
    return CreatedAtAction(nameof(GetById), new { id = userId }, new ApiResponse<object>(new { userId }));
}
```

---

### 1.8 🟡 Breakdown Photo Upload — Driver không có endpoint upload ảnh hỏng xe

**Vấn đề:** `POST /api/v1/drivers/{id}/report-breakdown` cần `photoUrls[]` nhưng không có endpoint lấy pre-signed URL cho breakdown photos. Driver phải tự có URL trước khi gọi API.

**Đề xuất:**
- Thêm query param vào endpoint đã có: `GET /api/v1/uploads/presigned-url?type=breakdown-photo`.
- Driver Service `MinIOStorageService` đã tồn tại — chỉ cần thêm case cho `breakdown-photo`.

---

### 1.9 🟢 TrustScore hiển thị trong Driver profile

**Vấn đề:** `TrustScore` có trong Driver aggregate nhưng `DriverDto` (Dapper read model) chưa include field này. Driver không biết điểm tín nhiệm của mình.

**Đề xuất:** Thêm `TrustScore` vào SQL query trong `GetDriverByIdQueryHandler` và `DriverDto`.

---

### 1.10 🟢 Pagination cho `GET /orders` — filter theo status/date

**Vấn đề:** `GET /api/v1/orders?customerId=` chưa có filter `status`, `dateFrom`, `dateTo` và chưa có `PagedResult` wrapper (chỉ trả array thuần).

**Đề xuất:** Update `ListOrdersByCustomerQueryHandler`:
```sql
SELECT o.Id, o.Status, o.CreatedAt, o.TotalWeightKg, o.ShipmentId, COUNT(*) OVER() AS TotalCount
FROM [order].orders o
WHERE o.CustomerId = @CustomerId
  AND (@Status IS NULL OR o.Status = @Status)
  AND (@DateFrom IS NULL OR o.CreatedAt >= @DateFrom)
  AND (@DateTo IS NULL OR o.CreatedAt <= @DateTo)
ORDER BY o.CreatedAt DESC
LIMIT @PageSize OFFSET @Offset
```

---

## 2. Kỹ Thuật (Technical)

### 2.1 🔴 [SECURITY] `PUT /drivers/{id}/status` thiếu Role Guard

**Vấn đề:** Endpoint hiện có `[Authorize]` nhưng thiếu role restriction. **Bất kỳ JWT hợp lệ nào** (kể cả Customer) đều có thể đổi trạng thái driver.

**Fix ngay:**
```csharp
// src/Services/Driver/TruckDelivery.Driver.Api/Controllers/DriversController.cs
[HttpPut("{id:guid}/status")]
[Authorize(Roles = "Admin,Driver")]  // THÊM role restriction
public async Task<IActionResult> UpdateStatus(Guid id, ...) { ... }
```

**Ngoài ra:** trong handler, cần check driver chỉ được đổi status của **chính mình** (không đổi được driver khác):
```csharp
// Nếu role = Driver → chỉ cho phép id == HttpContext.GetUserId()
// Nếu role = Admin → cho phép bất kỳ id
```

---

### 2.2 🔴 [SECURITY] Driver có thể set bất kỳ ShipmentStatus

**Vấn đề:** `PUT /api/v1/shipments/{id}/status` cho phép Driver set status sang `Failed`, `DriverAssigning`, `DispatcherReviewRequired`... Driver chỉ được set: `PickedUp`, `InTransit`, `Delivered`.

**Fix:**
```csharp
// src/Services/Shipment/TruckDelivery.Shipment.Application/Commands/UpdateShipmentStatus/
// UpdateShipmentStatusCommandHandler.cs
if (HttpContext.GetUserRole() == "Driver")
{
    var allowedStatuses = new[] { ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivered };
    if (!allowedStatuses.Contains(command.NewStatus))
        return Result.Failure(Error.Forbidden("Shipment.Status", "Driver can only set PickedUp, InTransit, or Delivered."));
}
```

---

### 2.3 🔴 Integration Tests — 0 tests tồn tại

**Vấn đề:** Các saga flows phức tạp (Dispatch, Breakdown) không có integration test. Nếu có thay đổi, regression không được phát hiện.

**Đề xuất — tạo test project:**
```
tests/Integration/
  TruckDelivery.Order.Integration.Tests/
  TruckDelivery.Shipment.Integration.Tests/
```

**Test cases ưu tiên:**

```csharp
// TruckDelivery.Shipment.Integration.Tests/DispatchSagaTests.cs
[Fact]
public async Task DispatchSaga_HappyPath_ShouldTransitionToInProgress()
{
    // Arrange: real MySQL (Testcontainers), real Kafka (Testcontainers)
    // Act: consume OrderCreatedEvent → DispatchSaga → DriverAssignmentRequested
    // Assert: Shipment.Status = DriverAssigning; OutboxMessage published
}

[Fact]
public async Task DispatchSaga_WhenRouteServiceUnreachable_ShouldUseHaversineFallback()
{
    // Arrange: mock routeServiceUrl → connection refused
    // Assert: Shipment vẫn transition qua RoutePlanning với Haversine distance
}

[Fact]
public async Task OrderCreatedConsumer_WhenDuplicateEvent_ShouldBeIdempotent()
{
    // Arrange: Publish OrderCreatedEvent 2 lần với same MessageId
    // Assert: Chỉ 1 Shipment được tạo
}
```

**Packages cần thêm:**
```xml
<PackageReference Include="Testcontainers" Version="3.*" />
<PackageReference Include="Testcontainers.MySql" Version="3.*" />
<PackageReference Include="Testcontainers.Kafka" Version="3.*" />
<PackageReference Include="Testcontainers.Redis" Version="3.*" />
```

---

### 2.4 🔴 Contract Tests — Event Schema không có bảo vệ

**Vấn đề:** Không có gì ngăn developer đổi tên field trong `OrderCreatedEvent` mà không update consumer. Kafka là schema-less → schema drift im lặng.

**Đề xuất — tạo test project:**
```
tests/Contract/
  TruckDelivery.EventContracts.Tests/
```

**Approach (PactNet hoặc custom JSON schema):**
```csharp
// Tests/Contract/Events/OrderCreatedEventContractTests.cs
[Fact]
public void OrderCreatedEvent_MustHave_RequiredFields()
{
    var @event = new OrderCreatedEvent
    {
        OrderId = Guid.NewGuid(), CustomerId = Guid.NewGuid(),
        TotalWeightKg = 50m, Items = [...],
        PickupLatitude = 10.76, PickupLongitude = 106.66,
        DeliveryLatitude = 21.02, DeliveryLongitude = 105.80
    };

    var json = JsonSerializer.Serialize(@event);
    var doc = JsonDocument.Parse(json);

    // Required fields (consumers depend on these)
    doc.RootElement.GetProperty("orderId").Should().NotBeNull();
    doc.RootElement.GetProperty("customerId").Should().NotBeNull();
    doc.RootElement.GetProperty("pickupLatitude").ValueKind.Should().NotBe(JsonValueKind.Undefined);
    doc.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
}
```

**Events cần test:**
- `OrderCreatedEvent` (field mới nhất: coordinates)
- `DriverAssignmentRequestedEvent`
- `DriverAssignedEvent`
- `ShipmentFailedEvent`
- `PaymentCompletedEvent`
- `VehicleBreakdownEvent`

---

### 2.5 🟡 OCR Docker Image — PaddleOCR Model không được bake vào image

**Vấn đề:** PaddleOCR download model (~900MB) lần đầu chạy. Trong production container orchestration (Kubernetes), pod restart = download lại. Cold start có thể mất 2–5 phút.

**Đề xuất — sửa Dockerfile:**
```dockerfile
# src/Services/OCR/truck-delivery-ocr/Dockerfile
FROM python:3.12-slim AS builder
WORKDIR /app
COPY pyproject.toml .
RUN pip install paddlepaddle paddleocr

# Pre-download models trong build stage
RUN python -c "
from paddleocr import PaddleOCR
PaddleOCR(use_angle_cls=True, lang='vi')
"

FROM python:3.12-slim
WORKDIR /app
# Copy pre-downloaded models từ builder
COPY --from=builder /root/.paddleocr /root/.paddleocr
COPY --from=builder /app /app
```

**Lưu ý:** Image size tăng ~1.5GB. Cần đăng ký Docker registry có quota phù hợp (GHCR có 500MB free, cần nâng lên package storage paid).

---

### 2.6 🟡 GitHub Actions — Thiếu `integration.yml` Workflow

**Vấn đề:** CI hiện chỉ có unit test gate (PR) và Docker publish (merge). Không có integration test gate trên merge to develop.

**Đề xuất — tạo `.github/workflows/integration.yml`:**
```yaml
name: Integration Tests
on:
  push:
    branches: [develop]

jobs:
  integration:
    runs-on: ubuntu-latest
    services:
      mysql:
        image: mysql:8.0
        env: { MYSQL_ROOT_PASSWORD: test, MYSQL_DATABASE: truck_test }
        ports: ['3306:3306']
        options: --health-cmd="mysqladmin ping" --health-interval=10s
      redis:
        image: redis:7-alpine
        ports: ['6379:6379']

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - run: dotnet restore TruckDelivery.slnx
      - run: dotnet build TruckDelivery.slnx --no-restore -c Release
      - run: dotnet test tests/Integration/ --no-build -c Release
```

---

### 2.7 🟡 Rate Limit GPS per-User thay vì per-IP

**Vấn đề:** `POST /api/v1/tracking/location` bị rate limit theo IP. 5 driver cùng 1 NAT gateway (công ty logistics) → 5 × 60 req/min = share quota → hit limit.

**Đề xuất:**
```csharp
// src/Services/Tracking/TruckDelivery.Tracking.Api/Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("gps-per-user", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,        // 1 request/giây
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Controller:
[EnableRateLimiting("gps-per-user")]
[HttpPost("location")]
```

---

### 2.8 🟡 Health Check Aggregate qua Gateway

**Vấn đề:** Không có endpoint tổng hợp health của toàn hệ thống qua Gateway. Ops team phải check từng service riêng lẻ.

**Đề xuất — thêm vào API Gateway:**
```csharp
// src/Gateway/TruckDelivery.Gateway/Program.cs
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://identity-service:8081/health"), "identity")
    .AddUrlGroup(new Uri("http://order-service:8082/health"), "order")
    .AddUrlGroup(new Uri("http://driver-service:8083/health"), "driver")
    .AddUrlGroup(new Uri("http://shipment-service:8086/health"), "shipment")
    .AddUrlGroup(new Uri("http://tracking-service:8087/health"), "tracking")
    .AddUrlGroup(new Uri("http://notification-service:8088/health"), "notification")
    .AddUrlGroup(new Uri("http://payment-service:8089/health"), "payment")
    .AddUrlGroup(new Uri("http://analytics-service:8095/health"), "analytics")
    .AddUrlGroup(new Uri("http://ocr-service:8090/health"), "ocr")
    .AddUrlGroup(new Uri("http://route-service:8084/health"), "route")
    .AddUrlGroup(new Uri("http://optimizer-service:8085/health"), "optimizer");

app.MapHealthChecks("/health/all", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

---

### 2.9 🟢 Verify `OrderDto` đã include `ShipmentId`

**Vấn đề:** `ShipmentId` đã được thêm vào Order aggregate, nhưng cần xác nhận Dapper query trong `GetOrderByIdQueryHandler` và `ListOrdersByCustomerQueryHandler` đã select `shipment_id` và map vào `OrderDto`.

**Kiểm tra:**
```sql
-- GetOrderByIdQueryHandler
SELECT o.Id, o.Status, o.CustomerId, o.ShipmentId, ... FROM orders o WHERE o.Id = @Id
```

Nếu `ShipmentId` chưa có trong DTO → Customer app không join được SignalR group.

---

### 2.10 🟢 Driver App — `GET /shipments/active?driverId=` Endpoint

**Vấn đề:** Driver app cần biết shipment hiện tại đang active khi mở app (app restart, token refresh). Hiện tại `GET /api/v1/shipments?driverId=&status=InProgress` có thể đã cover — cần verify endpoint có sẵn và có trong Gateway routing.

---

## 3. Bảo Mật (Security Hardening)

### 3.1 🔴 Đã nêu ở mục 2.1 — Role guard cho Driver status

### 3.2 🔴 Đã nêu ở mục 2.2 — Driver chỉ set PickedUp/InTransit/Delivered

### 3.3 🟡 Analytics Endpoints thiếu Defense-in-Depth

**Vấn đề:** Analytics endpoints (`/api/v1/analytics/*`) được bảo vệ bởi `[Authorize(Roles = "Admin")]` ở controller level, nhưng Gateway YARP route config không enforce role-based routing. Nếu YARP được bypass (nội bộ), bất kỳ request nào cũng tới Analytics.

**Đề xuất — thêm `AuthorizationPolicy` vào Gateway route:**
```json
// appsettings.json (YARP config)
"analytics-route": {
  "ClusterId": "analytics-cluster",
  "Match": { "Path": "/api/v1/analytics/{**catch-all}" },
  "Metadata": { "RequiredRole": "Admin" }
}
```

Hoặc giữ `[Authorize(Roles = "Admin")]` ở controller là đủ — chỉ cần đảm bảo Analytics service không expose port 8095 ra ngoài cluster (chỉ accessible qua Gateway).

---

### 3.4 🟡 IdCardNumber uniqueness — Race Condition

**Vấn đề:** `IdCardNumber UNIQUE` ở database level. Nhưng `SelfRegisterDriverCommandHandler` không check duplicate trước khi insert → DB sẽ throw exception thô, không phải `DomainException` được format đúng.

**Đề xuất:**
```csharp
// Handler: check trước khi create
var existing = await _driverRepo.FindByIdCardNumberAsync(command.IdCardNumber, ct);
if (existing is not null)
    return Result.Failure<Guid>(Error.Conflict("Driver.IdCard", "ID card number already registered."));
```

---

### 3.5 🟡 Refresh Token Rotation — Chưa rõ đã implement

**Vấn đề:** `RefreshToken` trong User aggregate tồn tại nhưng cần verify có Refresh Token Rotation không (mỗi lần refresh → invalidate token cũ, tạo token mới). Nếu không có rotation, stolen refresh token có thể dùng vô thời hạn.

**Kiểm tra:** `AuthController.RefreshToken` → `RefreshTokenCommandHandler` — có invalidate token cũ sau khi issue mới không?

---

### 3.6 🟢 Input Validation — Coordinate Range

**Vấn đề:** `AddressRequest.Latitude` và `AddressRequest.Longitude` không có validation range. Ai đó gửi `lat=999, lng=999` → Haversine formula vẫn tính, trả kết quả vô nghĩa.

**Đề xuất:**
```csharp
// CreateOrderCommandValidator.cs
RuleFor(x => x.PickupAddress.Latitude)
    .InclusiveBetween(-90, 90).When(x => x.PickupAddress.Latitude.HasValue);
RuleFor(x => x.PickupAddress.Longitude)
    .InclusiveBetween(-180, 180).When(x => x.PickupAddress.Longitude.HasValue);
```

---

## 4. Tài Liệu (Documentation)

### 4.1 🟡 Cập Nhật File Lỗi Thời

Các file viết ngày 2026-04-30 (trước nhiều Phase 2 fixes) hiện mô tả trạng thái không còn chính xác:

| File | Vấn đề | Hành động |
|---|---|---|
| `docs/api-gap-analysis.md` | 12/21 gaps đã được fix nhưng vẫn liệt kê là missing | Update status từng gap (Done / Still Missing) |
| `docs/architecture-business/09-implementation-status/01-as-built-status.md` | Ghi Order consumers MISSING, Gateway routes MISSING — đã fix | Update sang Done |
| `docs/architecture-business/09-implementation-status/03-gateway-gaps.md` | Ghi vehicle-route, analytics-route, ocr-route chưa có — đã thêm | Mark resolved |
| `CLAUDE.md` Remaining Gaps | Ghi "GitHub Actions missing" và "PhoneNumber missing" — đã được fix | Update section |
| `docs/api-reference.md` | Thiếu tất cả Phase 2 endpoints (drivers list, register, verify, etc.) | Append Phase 2 endpoints |

---

### 4.2 🟡 Thiếu Admin Portal Integration Guide

**Vấn đề:** Đã có `docs/mobile-integration/01-driver-app.md` và `02-customer-app.md` nhưng **không có guide cho Admin Portal** (NextJS Dashboard).

**Đề xuất — tạo `docs/mobile-integration/03-admin-portal.md`:**

Bao gồm:
- Admin login flow (JWT + refresh)
- Luồng xem danh sách shipments cần dispatch (polling vs SignalR)
- Confirm/Decline dispatch flow
- Driver verification queue (ManualReview)
- Fraud alert acknowledge flow
- KPI dashboard API calls
- SignalR admin group để nhận real-time alerts

---

### 4.3 🟡 Thiếu Database Schema / ER Diagram

**Vấn đề:** Không có tài liệu hóa schema các database. Developer mới phải đọc từng EFCore migration để hiểu quan hệ giữa các bảng.

**Đề xuất — tạo `docs/architecture-business/02-domain/database-schema.md`:**

Bao gồm ít nhất:
```
MySQL databases:
- truck_identity: users, refresh_tokens
- truck_order: orders, order_items
- truck_driver: drivers, vehicles, driver_swap_records, breakdown_reports
- truck_shipment: shipments, outbox_messages
- truck_notification: notification_records, device_tokens
- truck_payment: payments, escrow_payments

MongoDB:
- truck_tracking: tracking_sessions, tracking_points
- truck_analytics: breakdown_incidents, fraud_alerts
- Saga states: shipment_saga_states, breakdown_saga_states

PostGIS:
- driver_locations, road_network
```

---

### 4.4 🟢 Thiếu Production Deployment Guide

**Vấn đề:** Không có hướng dẫn deploy production. `docker/docker-compose.yml` tồn tại nhưng thiếu Kubernetes manifests, Helm charts, environment variable documentation.

**Đề xuất — tạo `docs/deployment/01-production-setup.md`:**
- Docker Compose cho local dev
- Environment variables cần thiết per service
- MinIO bucket setup (trucker-driver-docs, breakdown-photos)
- PaddleOCR model management
- Kafka topic creation (`kafka-topics.sh --create`)
- Database initialization (`dotnet ef database update` per service)

---

### 4.5 🟢 Thiếu MinIO / S3 Configuration Guide

**Vấn đề:** Driver document photos và breakdown photos được upload lên MinIO, nhưng không có tài liệu:
- MinIO bucket names
- IAM policy cho pre-signed URL
- CORS config cho browser upload
- Retention policy

**Đề xuất — tạo `docs/deployment/02-minio-setup.md`** với bucket policy, CORS config.

---

### 4.6 🟢 `docs/api-reference.md` cần bổ sung Phase 2 endpoints

**Endpoints mới từ Phase 2 chưa được document trong `api-reference.md`:**

```
POST /api/v1/auth/register/driver
POST /api/v1/drivers/register
GET  /api/v1/drivers?status=&page=
GET  /api/v1/drivers/pending-verification
POST /api/v1/drivers/{id}/verify
POST /api/v1/drivers/{id}/reject-verification
POST /api/v1/drivers/{id}/report-breakdown
GET  /api/v1/vehicles?status=&driverId=&page=
PUT  /api/v1/vehicles/{id}/status
GET  /api/v1/uploads/presigned-url?type=
GET  /api/v1/payments?status=&dateFrom=&page=
GET  /api/v1/payments/orders/{orderId}/escrow
POST /api/v1/notifications/register-device
POST /api/v1/analytics/fraud/alerts/{id}/acknowledge
POST /api/v1/shipments/{id}/decline-dispatch
GET  /api/v1/shipments?status=&customerId=&driverId=&page=
```

---

## 5. Ma Trận Ưu Tiên & Roadmap Đề Xuất

### Sprint 1 — Blocker & Security ✅ HOÀN THÀNH (2026-05-01)

| # | Hạng mục | Effort | Tác động | Status |
|---|---|---|---|---|
| 1 | Role guard `PUT /drivers/{id}/status` + driver-own-only check | XS (1h) | 🔴 Security bug | ✅ Done |
| 2 | Driver status restriction trong UpdateShipmentStatus | XS (2h) | 🔴 Security bug | ✅ Done |
| 3 | LicenseGrade filter trong Optimizer | S (4h) | 🔴 Dispatch eligibility | ✅ Done |
| 4 | SignalR `DriverAssigned` emit từ Tracking | S (3h) | 🔴 Driver không nhận việc | ✅ Done |
| 5 | Breakdown photo presigned URL (`type=breakdown-photo`) | XS (1h) | 🟡 Unblock driver flow | ✅ Done |
| 6 | Verify `OrderDto.ShipmentId` trong Dapper queries | XS (1h) | 🔴 Customer tracking | ✅ Done |
| 7 | Coordinate range validation trong FluentValidation | XS (1h) | 🟡 Data quality | ✅ Done |
| 8 | IdCardNumber duplicate check trong handler | XS (1h) | 🟡 UX error message | ✅ Done |

---

### Sprint 2 — Notifications & Payment (2–3 tuần)

| # | Hạng mục | Effort | Tác động |
|---|---|---|---|
| 9 | Firebase FCM real push sender | M (3 ngày) | 🔴 Real-time alerts |
| 10 | Twilio SMS sender | S (1 ngày) | 🟡 Fallback notification |
| 11 | VNPay gateway integration | L (5 ngày) | 🔴 Doanh thu thực |
| 12 | Admin notification khi ManualReview | S (1 ngày) | 🟡 Operations |
| 13 | `POST /api/v1/admin/accounts` | S (1 ngày) | 🟡 Admin management |
| 14 | TrustScore trong DriverDto | XS (1h) | 🟢 Driver UX |
| 15 | Pagination `GET /orders` (status/date filter) | S (1 ngày) | 🟡 Customer UX |

---

### Sprint 3 — Testing & Quality Gate (2–3 tuần)

| # | Hạng mục | Effort | Tác động |
|---|---|---|---|
| 16 | Integration test: Dispatch saga happy path | M (3 ngày) | 🔴 Regression safety |
| 17 | Integration test: Order consumer idempotency | S (1 ngày) | 🔴 Data correctness |
| 18 | Contract tests: 6 Kafka event schemas | S (2 ngày) | 🔴 Schema drift |
| 19 | GitHub Actions `integration.yml` | S (1 ngày) | 🔴 CI gate |
| 20 | Rate limit GPS per-user | XS (2h) | 🟡 Scalability |
| 21 | Health check aggregate qua Gateway | S (2h) | 🟢 Observability |

---

### Sprint 4 — Production Readiness (1–2 tuần)

| # | Hạng mục | Effort | Tác động |
|---|---|---|---|
| 22 | OCR Docker image bake PaddleOCR model | S (1 ngày) | 🟡 Cold start |
| 23 | SMTP email sender | S (1 ngày) | 🟢 Notification |
| 24 | Refresh token rotation verify | XS (2h) | 🟡 Security |
| 25 | Analytics role enforcement at Gateway | XS (1h) | 🟡 Defense-in-depth |
| 26 | `integration.yml` CI trigger on develop merge | XS (1h) | ✅ Automation |

---

### Documentation Sprint (song song với Sprint 1-2)

| # | Hạng mục | Effort |
|---|---|---|
| 27 | Cập nhật `docs/api-gap-analysis.md` (đánh dấu gaps đã fix) | S |
| 28 | Update `docs/architecture-business/09-implementation-status/01-as-built-status.md` | S |
| 29 | Tạo `docs/mobile-integration/03-admin-portal.md` | M |
| 30 | Tạo `docs/architecture-business/02-domain/database-schema.md` | M |
| 31 | Bổ sung Phase 2 endpoints vào `docs/api-reference.md` | S |
| 32 | Tạo `docs/deployment/01-production-setup.md` | M |
| 33 | Tạo `docs/deployment/02-minio-setup.md` | S |
| 34 | Update `CLAUDE.md` Remaining Gaps | XS |

---

## 6. Tổng Hợp Gap Status (updated 2026-05-01)

### Business / Feature Gaps

| Gap | Mô tả | Sprint | Status |
|---|---|---|---|
| G-B1 | LicenseGrade filter trong Optimizer | Sprint 1 | ✅ Done |
| G-B2 | Notification senders là stubs | Sprint 2 | ⏳ Pending |
| G-B3 | Payment gateway chỉ có COD | Sprint 2 | ⏳ Pending |
| G-B4 | Admin notification khi ManualReview | Sprint 2 | ⏳ Pending |
| G-B5 | SignalR `DriverAssigned` từ Tracking hub | Sprint 1 | ✅ Done |
| G-B6 | Breakdown photo presigned URL | Sprint 1 | ✅ Done |
| G-B7 | TrustScore trong DriverDto | Sprint 2 | ⏳ Pending |
| G-B8 | Pagination `GET /orders` với date/status filter | Sprint 2 | ⏳ Pending |
| G-B9 | `POST /api/v1/admin/accounts` | Sprint 2 | ⏳ Pending |
| G-B10 | Verify `OrderDto.ShipmentId` trong Dapper | Sprint 1 | ✅ Done |

### Security Gaps

| Gap | Mô tả | Sprint | Status |
|---|---|---|---|
| G-S1 | `PUT /drivers/{id}/status` thiếu role guard | Sprint 1 | ✅ Done |
| G-S2 | Driver có thể set bất kỳ ShipmentStatus | Sprint 1 | ✅ Done |
| G-S3 | IdCardNumber duplicate — error không format đúng | Sprint 1 | ✅ Done |
| G-S4 | Coordinate range validation chưa có | Sprint 1 | ✅ Done |
| G-S5 | Refresh token rotation — cần verify | Sprint 4 | ⏳ Pending |
| G-S6 | Analytics endpoints — defense-in-depth | Sprint 4 | ⏳ Pending |

### Technical / Infrastructure Gaps

| Gap | Mô tả | Sprint | Status |
|---|---|---|---|
| G-T1 | 0 integration tests | Sprint 3 | ⏳ Pending |
| G-T2 | 0 contract tests | Sprint 3 | ⏳ Pending |
| G-T3 | `integration.yml` CI workflow chưa có | Sprint 3 | ⏳ Pending |
| G-T4 | OCR Docker image — model chưa bake | Sprint 4 | ⏳ Pending |
| G-T5 | Rate limit GPS per-IP (nên per-user) | Sprint 3 | ⏳ Pending |
| G-T6 | Health check aggregate qua Gateway | Sprint 3 | ⏳ Pending |

### Documentation Gaps

| Gap | Mô tả | Sprint | Status |
|---|---|---|---|
| G-D1 | `api-gap-analysis.md` lỗi thời | Doc Sprint | ⏳ Pending |
| G-D2 | `as-built-status.md` lỗi thời | Doc Sprint | ⏳ Pending |
| G-D3 | Thiếu Admin Portal integration guide | Doc Sprint | ⏳ Pending |
| G-D4 | Thiếu Database schema diagram | Doc Sprint | ⏳ Pending |
| G-D5 | `api-reference.md` thiếu Phase 2 endpoints | Doc Sprint | ⏳ Pending |
| G-D6 | Thiếu Production deployment guide | Doc Sprint | ⏳ Pending |
| G-D7 | Thiếu MinIO setup guide | Doc Sprint | ⏳ Pending |

---

**Tổng: 28 gaps — Sprint 1: 8/8 ✅ — Còn lại: 20 gaps (Sprint 2, 3, 4, Doc Sprint)**

Sprint 2 tiếp theo: G-B2 (FCM notification), G-B3 (VNPay), G-B4 (ManualReview admin notify), G-B7 (TrustScore DTO), G-B8 (Order pagination), G-B9 (Admin accounts).
