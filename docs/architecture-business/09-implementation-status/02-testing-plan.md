# Testing Plan — From Zero

> Cập nhật: 2026-04-30 | Hiện trạng: **0 tests**
>
> Tech stack: xUnit + FluentAssertions + Testcontainers (.NET 10)
> **Test projects thêm vào `TruckDelivery.slnx` — không tạo file `.sln` mới.**

---

## 1. Mục tiêu

| Milestone | Target | Timeline |
|---|---|---|
| M1 — Domain baseline | Unit tests cho domain models ≥ 70% coverage | Tuần 4–5 |
| M2 — Integration core | Happy path + consumers + idempotency | Tuần 5–6 |
| M3 — Contract safety | Event schemas không break silently | Tuần 6–7 |
| M4 — CI gate | PR tự động fail nếu unit test fails | Tuần 7–8 |

---

## 2. Cấu trúc Test Projects

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
    TruckDelivery.Tracking.Integration.Tests/
  Contract/
    TruckDelivery.EventContracts.Tests/
src/Services/OCR/truck-delivery-ocr/
  tests/                ← pytest (Python, riêng biệt — không thuộc .slnx)
    unit/
      test_cccd_extraction.py
      test_license_extraction.py
      test_vehicle_reg_extraction.py
      test_confidence_scoring.py
      test_cross_checks.py
    integration/
      test_ocr_kafka_consumer.py
```

> .NET project files là `.csproj`, thêm vào `TruckDelivery.slnx` bằng `dotnet sln add`.
> OCR Python tests chạy độc lập bằng `pytest` — không liên quan đến `TruckDelivery.slnx`.

### 2.1 Shared packages (Directory.Packages.props hoặc per-project)

```xml
<PackageReference Include="xunit" Version="2.9.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
<PackageReference Include="FluentAssertions" Version="7.*" />
<PackageReference Include="Moq" Version="4.20.*" />
<!-- Integration only: -->
<PackageReference Include="Testcontainers" Version="3.*" />
<PackageReference Include="Testcontainers.MySql" Version="3.*" />
<PackageReference Include="Testcontainers.Kafka" Version="3.*" />
<PackageReference Include="Testcontainers.Redis" Version="3.*" />
<PackageReference Include="Testcontainers.MongoDb" Version="3.*" />
```

---

## 3. Unit Tests — Domain Layer

### 3.1 Order Domain (Ưu tiên 1)

**File:** `tests/Unit/TruckDelivery.Order.Domain.Tests/OrderTests.cs`

```csharp
public class OrderTests
{
    // --- State machine (valid paths) ---
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.AssignedToDriver)]
    [InlineData(OrderStatus.AssignedToDriver, OrderStatus.PickedUp)]
    [InlineData(OrderStatus.PickedUp, OrderStatus.InTransit)]
    [InlineData(OrderStatus.InTransit, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Completed)]
    public void Should_AllowTransition_WhenValidPath(OrderStatus from, OrderStatus to)

    // --- State machine (invalid transitions) ---
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]    // jump
    [InlineData(OrderStatus.Completed, OrderStatus.Pending)]    // backward
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]  // from terminal
    public void Should_ThrowDomainException_WhenTransitionIsInvalid(OrderStatus from, OrderStatus to)

    // --- Cancel guards ---
    [Fact]
    public void Cancel_Should_Succeed_WhenStatusIsPending()

    [Fact]
    public void Cancel_Should_Succeed_WhenStatusIsConfirmed()

    [Fact]
    public void Cancel_Should_Fail_WhenOrderIsInTransit()

    [Fact]
    public void Cancel_Should_Fail_WhenOrderIsDelivered()

    // --- ShipmentId update ---
    [Fact]
    public void SetShipmentId_Should_PersistValue_WhenDriverAssigned()

    // --- Factory ---
    [Fact]
    public void Create_Should_SetStatusToPending_AndRaiseDomainEvent()

    [Fact]
    public void Create_Should_Fail_WhenPickupSameAsDelivery()

    [Fact]
    public void Create_Should_Fail_WhenItemsEmpty()
}
```

---

### 3.2 Driver Domain (Ưu tiên 1)

**File:** `tests/Unit/TruckDelivery.Driver.Domain.Tests/DriverTests.cs`

```csharp
public class DriverTests
{
    // --- LicenseGrade validation ---
    [Theory]
    [InlineData(LicenseGrade.B1)]
    [InlineData(LicenseGrade.E)]
    public void Create_Should_Fail_WhenLicenseGradeNotEligibleForTruckDelivery(LicenseGrade grade)

    [Theory]
    [InlineData(LicenseGrade.B2)]
    [InlineData(LicenseGrade.C)]
    [InlineData(LicenseGrade.FC)]
    [InlineData(LicenseGrade.FD)]
    public void Create_Should_Succeed_WhenLicenseGradeEligible(LicenseGrade grade)

    [Fact]
    public void Create_Should_Fail_WhenLicenseExpired()

    // --- TrustScore ---
    [Fact]
    public void ReportBreakdown_Should_DeductTrustScore_By3()

    [Fact]
    public void ReportBreakdown_Should_SetStatusOffline()

    [Fact]
    public void TrustScore_Should_NotGoBelowZero_WhenMultipleDeductions()

    // --- Anti-fraud guard ---
    [Fact]
    public void ReportBreakdown_Should_Fail_WhenTrustScoreBelow30()

    // --- Assign vehicle ---
    [Fact]
    public void AssignVehicle_Should_Fail_WhenDriverIsInactive()

    // --- Status transitions ---
    [Fact]
    public void UpdateStatus_Should_Fail_WhenDriverIsInactive()

    // --- Verification guard ---
    [Theory]
    [InlineData(DriverVerificationStatus.Draft)]
    [InlineData(DriverVerificationStatus.PendingOcrVerification)]
    [InlineData(DriverVerificationStatus.ManualReview)]
    [InlineData(DriverVerificationStatus.Rejected)]
    public void SetAvailable_Should_Fail_WhenNotVerified(DriverVerificationStatus status)
    // Driver với VerificationStatus ≠ OcrVerified | AdminVerified
    // UpdateStatus(Available) → Result.Failure với "Driver.Verification" error code

    [Theory]
    [InlineData(DriverVerificationStatus.OcrVerified)]
    [InlineData(DriverVerificationStatus.AdminVerified)]
    public void SetAvailable_Should_Succeed_WhenVerified(DriverVerificationStatus status)

    // --- OCR verification status transitions ---
    [Fact]
    public void ApplyOcrResult_Should_SetOcrVerified_WhenConfidenceAbove085()

    [Fact]
    public void ApplyOcrResult_Should_SetManualReview_WhenConfidenceBetween065And085()

    [Fact]
    public void ApplyOcrResult_Should_SetRejected_WhenConfidenceBelow065()

    [Fact]
    public void AdminVerify_Should_Fail_WhenNotInManualReviewOrPending()

    [Fact]
    public void AdminReject_Should_SetStatusRejected_AndStoreNotes()
}

public class LicenseGradeVehicleCompatibilityTests
{
    [Theory]
    [InlineData(LicenseGrade.B2, VehicleType.Motorbike, true)]
    [InlineData(LicenseGrade.B2, VehicleType.Van, true)]
    [InlineData(LicenseGrade.B2, VehicleType.Truck3T, false)]
    [InlineData(LicenseGrade.C, VehicleType.Truck3T, true)]
    [InlineData(LicenseGrade.C, VehicleType.Truck5T, true)]
    [InlineData(LicenseGrade.C, VehicleType.Truck10T, true)]
    [InlineData(LicenseGrade.C, VehicleType.Truck15T, false)]
    [InlineData(LicenseGrade.FC, VehicleType.Truck15T, true)]
    [InlineData(LicenseGrade.FD, VehicleType.Truck15T, true)]
    public void IsCompatible_Should_ReturnExpected(LicenseGrade grade, VehicleType vehicleType, bool expected)
}
```

---

### 3.3 Shipment Domain (Ưu tiên 2)

**File:** `tests/Unit/TruckDelivery.Shipment.Domain.Tests/ShipmentTests.cs`

```csharp
public class ShipmentTests
{
    [Fact]
    public void MarkReassigning_Should_CaptureOriginalDriverId_BeforeClearing()

    [Fact]
    public void MarkReassigning_Should_Fail_WhenNotInProgress()

    [Fact]
    public void Assign_Should_Fail_WhenShipmentNotInDriverAssigningState()

    [Fact]
    public void Complete_Should_Fail_WhenNotInProgress()

    [Fact]
    public void MarkDispatcherReviewRequired_Should_Succeed_WhenInDriverAssigning()

    // Dispatch flow
    [Fact]
    public void ConfirmDispatch_Should_Succeed_WhenInDispatcherReviewRequired()

    [Fact]
    public void DeclineDispatch_Should_TransitionToFailed()

    [Fact]
    public void DeclineDispatch_Should_Fail_WhenNotInDispatcherReviewRequired()
}
```

---

### 3.4 Vehicle Domain (Ưu tiên 2)

```csharp
public class VehicleTests
{
    [Fact]
    public void Create_Should_Fail_WhenDimensionsNotProvided()

    [Fact]
    public void Create_Should_Fail_WhenRegistrationExpired()

    [Fact]
    public void SetMaintenance_Should_Fail_WhenDriverAssigned()

    [Fact]
    public void AssignDriver_Should_Fail_WhenUnderMaintenance()

    [Fact]
    public void AssignDriver_Should_Fail_WhenBreakdown()
}
```

---

### 3.5 Payment Domain (Ưu tiên 2)

```csharp
public class PaymentTests
{
    [Fact]
    public void Create_Should_SetStatusCreated()

    [Fact]
    public void Complete_Should_Fail_WhenNotAuthorized()
}

public class EscrowPaymentTests
{
    [Fact]
    public void Confirm_Should_ReleaseEscrow_WhenLocked()

    [Fact]
    public void Dispute_Should_TransitionToDisputed()

    [Fact]
    public void Confirm_Should_Fail_WhenAlreadyReleased()

    [Fact]
    public void EscrowAmount_Should_Be50000VND()
}
```

---

## 4. Integration Tests

### 4.1 Shared Fixture

```csharp
// tests/Integration/IntegrationTestFixture.cs
public sealed class IntegrationTestFixture : IAsyncLifetime
{
    public MySqlContainer MySql { get; } = new MySqlBuilder()
        .WithDatabase("truck_order_test")
        .Build();

    public KafkaContainer Kafka { get; } = new KafkaBuilder().Build();

    public RedisContainer Redis { get; } = new RedisBuilder().Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(MySql.StartAsync(), Kafka.StartAsync(), Redis.StartAsync());
        // Run EF migrations
        await RunMigrationsAsync();
    }

    public async Task DisposeAsync()
        => await Task.WhenAll(MySql.DisposeAsync().AsTask(),
                              Kafka.DisposeAsync().AsTask(),
                              Redis.DisposeAsync().AsTask());
}
```

---

### 4.2 Order Integration Tests (Ưu tiên 1 — sau khi fix consumers)

```csharp
public class OrderIntegrationTests(IntegrationTestFixture fixture)
    : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task CreateOrder_Should_PersistToMySQL_And_PublishKafkaEvent()
    // Assert: orders table có row
    // Assert: Kafka "order.order.created" có message với orderId đúng

    [Fact]
    public async Task OrderAssignedConsumer_Should_UpdateStatusToAssignedToDriver()
    // Produce DriverAssignedEvent → Kafka
    // Wait consumer process (max 5s)
    // Assert: order.Status = AssignedToDriver
    // Assert: order.ShipmentId = shipmentId từ event

    [Fact]
    public async Task OrderAssignedConsumer_Should_Skip_DuplicateEvent()
    // Produce cùng event 2 lần (same MessageId)
    // Assert: status update chỉ 1 lần

    [Fact]
    public async Task ShipmentCompletedConsumer_Should_UpdateStatusToDelivered()

    [Fact]
    public async Task PaymentCompletedConsumer_Should_UpdateStatusToCompleted()

    [Fact]
    public async Task CancelOrder_Should_Fail_WhenStatusIsInTransit()
}
```

---

### 4.3 Driver Registration Integration Tests (Ưu tiên 2)

```csharp
public class DriverRegistrationIntegrationTests
{
    [Fact]
    public async Task SelfRegister_Should_CreateDriverProfile_WithVehicle()
    // POST /api/v1/drivers/register với đầy đủ fields
    // Assert: Driver record trong DB với LicenseGrade, DOB, Address
    // Assert: Vehicle record với dimensions, registration

    [Fact]
    public async Task SelfRegister_Should_Fail_WhenLicenseGradeB1()

    [Fact]
    public async Task SelfRegister_Should_Fail_WhenLicenseExpired()

    [Fact]
    public async Task SelfRegister_Should_Fail_WhenVehicleIncompatibleWithLicenseGrade()
    // B2 + Truck3T → fail
    // C + Motorbike → fail

    [Fact]
    public async Task AdminSeeder_Should_CreateAdminOnFirstRun()
    // Chạy seeder → admin account tồn tại
    // Chạy lại → không duplicate
}
```

---

### 4.4 Dispatch Saga Integration Tests (Ưu tiên 2)

```csharp
public class DispatchSagaIntegrationTests
{
    [Fact]
    public async Task OrderCreated_Should_TriggerSaga_And_PublishDriverAssignmentRequest()

    [Fact]
    public async Task ConfirmDispatch_Should_Succeed_WhenInDispatcherReviewRequired()

    [Fact]
    public async Task DeclineDispatch_Should_FailShipment_And_NotifyCustomer()
    // POST /decline-dispatch
    // Assert: Shipment.Status = Failed
    // Assert: ShipmentFailedEvent published to Kafka
    // Assert: Notification consumer receives event

    [Fact]
    public async Task BreakdownSaga_Should_Reassign_WhenDriverReportsBreakdown()
}
```

---

## 4.5 OCR Service Tests (Python pytest)

### Unit Tests — Extraction Logic

```python
# tests/unit/test_cccd_extraction.py

def test_extract_cccd_front_returns_id_number():
    """OCR trả đúng số CCCD 12 chữ số từ ảnh mẫu."""
    result = extract_id_card(front_url=SAMPLE_CCCD_FRONT, back_url=SAMPLE_CCCD_BACK)
    assert result.id_number == "079123456789"
    assert len(result.id_number) == 12

def test_extract_cccd_returns_normalized_name():
    """Tên được normalize về titlecase để match với input của driver."""
    result = extract_id_card(front_url=SAMPLE_CCCD_FRONT, back_url=SAMPLE_CCCD_BACK)
    assert result.full_name == "Nguyễn Văn A"  # không phải "NGUYỄN VĂN A"

def test_extract_cccd_returns_confidence_score():
    result = extract_id_card(front_url=SAMPLE_CCCD_FRONT, back_url=SAMPLE_CCCD_BACK)
    assert 0.0 <= result.confidence <= 1.0


# tests/unit/test_license_extraction.py

def test_extract_license_returns_grade_enum():
    result = extract_license(front_url=SAMPLE_GPLX_FRONT, back_url=SAMPLE_GPLX_BACK)
    assert result.license_grade in ["B1", "B2", "C", "D", "E", "FC", "FD"]

def test_extract_license_returns_valid_expiry_date():
    result = extract_license(front_url=SAMPLE_GPLX_FRONT, back_url=SAMPLE_GPLX_BACK)
    assert result.expiry_date > date.today()


# tests/unit/test_confidence_scoring.py

def test_confidence_above_085_returns_ocr_verified():
    verdict = compute_verification_verdict(confidence=0.90, submitted=mock_data)
    assert verdict.status == "OcrVerified"

def test_confidence_between_065_and_085_returns_manual_review():
    verdict = compute_verification_verdict(confidence=0.75, submitted=mock_data)
    assert verdict.status == "ManualReview"

def test_confidence_below_065_returns_rejected():
    verdict = compute_verification_verdict(confidence=0.50, submitted=mock_data)
    assert verdict.status == "Rejected"


# tests/unit/test_cross_checks.py

def test_cross_check_name_mismatch_reduces_confidence():
    """Tên trên CCCD không khớp với GPLX → giảm confidence."""
    cccd = CCCDExtraction(full_name="Nguyễn Văn A", ...)
    gplx = LicenseExtraction(full_name="Nguyễn Văn B", ...)
    score = cross_check(cccd, gplx, submitted_data)
    assert score < 0.85  # name mismatch penalty applied

def test_cross_check_dob_mismatch_reduces_confidence():
    """Ngày sinh trên CCCD không khớp với input driver → giảm confidence."""

def test_cross_check_owner_id_mismatch_flags_vehicle_reg():
    """Số CCCD chủ xe trên giấy đăng ký không khớp với số CCCD tài xế → flag ManualReview."""

def test_cross_check_all_match_returns_high_confidence():
    """Tất cả fields khớp → confidence ≥ 0.85."""
```

### Integration Tests — Kafka Consumer

```python
# tests/integration/test_ocr_kafka_consumer.py

async def test_consumer_processes_driver_documents_submitted_event():
    """OCR consumer nhận event → chạy verify → publish DriverVerificationCompletedEvent."""
    # Produce DriverDocumentsSubmittedEvent với photo URLs hợp lệ
    # Wait consumer process (max 10s)
    # Assert: DriverVerificationCompletedEvent xuất hiện trên ocr.driver.verification-completed
    # Assert: event chứa driverId, confidence, status

async def test_consumer_skips_duplicate_event_idempotency():
    """Cùng MessageId gửi 2 lần → chỉ xử lý 1 lần."""

async def test_consumer_publishes_manual_review_when_confidence_low():
    """Ảnh quality thấp → status = ManualReview trong event."""
```

---

## 5. Contract Tests — Event Schema

**Mục đích:** Ngăn producer thay đổi event schema mà consumer không biết.

```csharp
// tests/Contract/TruckDelivery.EventContracts.Tests/
public class OrderCreatedEventContractTests
{
    [Fact]
    public void Serialize_Should_ContainAllRequiredFields()
    {
        var @event = new OrderCreatedEvent { MessageId = Guid.NewGuid(), OrderId = Guid.NewGuid(), /* ... */ };
        var json = JsonSerializer.Serialize(@event);
        var doc = JsonDocument.Parse(json);

        // Consumer-side required fields:
        doc.RootElement.GetProperty("messageId").GetGuid().Should().NotBeEmpty();
        doc.RootElement.GetProperty("orderId").GetGuid().Should().NotBeEmpty();
        doc.RootElement.GetProperty("customerId").GetGuid().Should().NotBeEmpty();
        doc.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetProperty("schemaVersion").GetInt32().Should().BePositive();
    }

    [Fact]
    public void Deserialize_Should_Succeed_FromMinimalJson()
    {
        // Backward compat: consumer cũ vẫn deserialize được khi producer thêm field mới
        const string minimalJson = """{"messageId":"...", "orderId":"...", "customerId":"...", "items":[]}""";
        var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(minimalJson);
        @event.Should().NotBeNull();
    }
}
```

**Events cần có contract test:**

| Event | Priority |
|---|---|
| `OrderCreatedEvent` | P1 |
| `DriverAssignedEvent` (từ Shipment) | P1 |
| `ShipmentStartedEvent` | P1 |
| `ShipmentCompletedEvent` | P1 |
| `PaymentCompletedEvent` | P1 |
| `DriverDocumentsSubmittedEvent` | P1 (OCR consumer phụ thuộc) |
| `DriverVerificationCompletedEvent` | P1 (Driver service consumer phụ thuộc) |
| `VehicleBreakdownEvent` | P2 |
| `BreakdownReassignmentCompletedEvent` | P2 |
| `SuspiciousDriverPairDetectedEvent` | P2 |

---

## 6. Coverage Targets

| Layer | Target | Phạm vi |
|---|---|---|
| Domain aggregates | ≥ 85% | Order, Driver, Shipment, Payment, Vehicle |
| Domain LicenseGrade validation | 100% | Tất cả cases eligibility + compatibility |
| DriverVerificationStatus guard | 100% | Tất cả status transitions + Available guard |
| Kafka consumers (idempotency) | ≥ 90% | Order consumers (3), critical consumers |
| Application handlers | ≥ 60% | Commands có side effects |
| Infrastructure repositories | Không unit test | Integration tests thay thế |
| API controllers | ≥ 40% | Happy paths + error cases |
| OCR extraction (Python) | ≥ 80% | CCCD, GPLX, đăng ký xe |
| OCR confidence scoring + cross-check | 100% | Tất cả threshold cases |
| OCR Kafka consumer (idempotency) | ≥ 90% | Duplicate MessageId |

---

## 7. Test Execution

### Local

```bash
# Unit (< 10 giây)
dotnet test tests/Unit/ --no-build --logger "console;verbosity=minimal"

# Integration (30–90 giây — Testcontainers spin-up)
dotnet test tests/Integration/ --no-build

# All
dotnet test TruckDelivery.slnx --no-build
```

### CI Strategy

```yaml
# PR gate (< 2 phút):
- dotnet build
- dotnet test tests/Unit/

# On merge to develop (< 5 phút):
- dotnet test tests/Unit/
- dotnet test tests/Integration/

# Nightly:
- dotnet test tests/Contract/
- Full suite
```

---

## 8. Test-First Rule cho Phase 1 Fixes

Khi implement **Order Consumers** (fix critical bug):

```
1. Viết failing test:
   OrderAssignedConsumer_Should_UpdateStatusToAssignedToDriver ← RED

2. Implement OrderAssignedConsumer.cs

3. Test pass ← GREEN

4. Refactor nếu cần

5. Commit với test + implementation cùng lúc
```

**Không merge consumer code nào không có integration test đi kèm.**

---

## 9. Test Data Strategy

### Driver Test Data

```csharp
public static class TestDrivers
{
    // Valid drivers với license grade hợp lệ
    public static Driver CreateValidTruck3TDriver() =>
        Driver.Create(Guid.NewGuid(), "driver@test.vn", "Nguyễn", "Văn A",
            "0901234567", "079123456", LicenseGrade.C,
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
            DateOnly.Parse("1985-06-15"),
            "123 Test St, Q.1, TP.HCM").Value;

    public static Driver CreateExpiredLicenseDriver() =>
        Driver.Create(Guid.NewGuid(), "expired@test.vn", "Test", "Expired",
            "0901234568", "079654321", LicenseGrade.C,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), // expired
            DateOnly.Parse("1990-01-01"), "Address").Value; // will fail
}
```

### Admin Seed Verification

```csharp
[Fact]
public async Task AdminSeeder_Should_NotCreateDuplicate_WhenRunTwice()
{
    await AdminSeeder.SeedAsync(_dbContext);
    await AdminSeeder.SeedAsync(_dbContext); // chạy lần 2

    var adminCount = await _dbContext.Users
        .CountAsync(u => u.Role == UserRole.Admin);

    adminCount.Should().Be(1);
}
```
