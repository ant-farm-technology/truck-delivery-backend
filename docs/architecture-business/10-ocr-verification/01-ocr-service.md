# OCR Verification Service

> Service: `truck-delivery-ocr` | Port: :8090
> Technology: Python 3.12 + FastAPI + PaddleOCR + Kafka consumer
> Mục đích: Xác minh tài liệu tài xế — đọc, trích xuất và so sánh thông tin giấy tờ

---

## 1. Lý do tồn tại

Driver registration yêu cầu upload 7 ảnh giấy tờ:
- Ảnh chân dung (1)
- CCCD/CMND 2 mặt (2)
- Giấy phép lái xe 2 mặt (2)
- Giấy đăng ký xe 2 mặt (2)

Hệ thống cần đảm bảo:
1. **Auto-fill** — OCR đọc giấy tờ → trả dữ liệu về client → client pre-fill form → UX tốt hơn, ít lỗi nhập
2. **Verify** — So sánh dữ liệu OCR với dữ liệu driver tự nhập → phát hiện bất khớp/giả mạo
3. **Async queue** — Không block registration response. Driver submit → OCR chạy nền → cập nhật verification status

---

## 2. Workflow Tổng Quan (Hybrid: Auto-fill + Async Verify)

```
┌─────────────────────────────────────────────────────────────┐
│  Phase A — Auto-fill (real-time, client-facing)             │
│                                                             │
│  1. Driver app upload ảnh → S3/MinIO via pre-signed URL     │
│  2. Driver app → POST /api/v1/ocr/extract/id-card           │
│     → OCR trả về: { full_name, dob, address, id_number }   │
│  3. Driver app → POST /api/v1/ocr/extract/license           │
│     → OCR trả về: { license_number, grade, expiry }        │
│  4. Driver app → POST /api/v1/ocr/extract/vehicle-reg       │
│     → OCR trả về: { plate, brand, model, reg_number }      │
│  5. Client pre-fills form, driver review/chỉnh sửa         │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Phase B — Submit + Async Verify                            │
│                                                             │
│  6. Driver submit: POST /drivers/register                   │
│     (all data + photo URLs)                                 │
│  7. Driver service: tạo Driver (VerificationStatus=Pending) │
│  8. Driver service: publish DriverDocumentsSubmittedEvent   │
│     → Kafka: driver.documents.submitted                     │
│  9. OCR service: consume event → re-extract all docs        │
│     → compare extracted vs submitted data                  │
│ 10. OCR service: publish DriverVerificationCompletedEvent   │
│     → Kafka: ocr.driver.verification-completed             │
│ 11. Driver service: consume event                           │
│     → confidence ≥ 0.85 → VerificationStatus = OcrVerified │
│     → confidence < 0.85 → ManualReview (Admin notified)    │
│ 12. Driver chưa Verified → KHÔNG thể set status Available  │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. OCR Engine — PaddleOCR

**Lý do chọn PaddleOCR:**
- Hỗ trợ tiếng Việt out-of-the-box (model `vi` pre-trained)
- Tốt nhất cho CCCD/CMND format chuẩn Việt Nam (chip-based sau 2021)
- Độ chính xác cao hơn Tesseract với tiếng Việt có dấu
- Open-source, deploy on-premise (không gửi data ra ngoài)

**Thư viện:**
```
paddlepaddle>=3.0 (CPU version cho deployment đơn giản)
paddleocr>=2.9
```

> **Note về GPU:** Nên dùng `paddlepaddle-gpu` trong production nếu có GPU, xử lý nhanh hơn ~10x. Môi trường dev dùng CPU là đủ.

---

## 4. Dữ liệu trích xuất — Document Schemas

### 4.1 CCCD/CMND

```python
class CCCDExtraction(BaseModel):
    id_number: str              # Số CCCD (12 chữ số)
    full_name: str              # Họ và tên (uppercase trên giấy tờ)
    date_of_birth: date         # Ngày sinh
    gender: str                 # Nam / Nữ
    nationality: str            # Quốc tịch (default: Việt Nam)
    place_of_origin: str        # Quê quán
    place_of_residence: str     # Nơi thường trú
    expiry_date: date           # Có giá trị đến
    confidence: float           # 0.0–1.0 (weighted avg across fields)
    raw_text: str               # Raw OCR text (cho debug)
```

**Fields dùng để verify với Driver input:**
- `id_number` → verify format + uniqueness
- `full_name` → compare với `firstName + lastName`
- `date_of_birth` → compare với `dateOfBirth`
- `place_of_residence` → compare (fuzzy match) với `address`

### 4.2 Giấy Phép Lái Xe (GPLX)

```python
class LicenseExtraction(BaseModel):
    license_number: str         # Số GPLX
    full_name: str              # Họ và tên
    date_of_birth: date         # Ngày sinh
    address: str                # Nơi cư trú
    license_grade: str          # Hạng: "B1" | "B2" | "C" | "D" | "E" | "FC" | "FD"
    issue_date: date            # Ngày cấp
    expiry_date: date           # Ngày hết hạn
    issuing_authority: str      # Cơ quan cấp (Sở GTVT ...)
    confidence: float
    raw_text: str
```

**Fields dùng để verify:**
- `license_number` → compare với `licenseNumber`
- `license_grade` → compare với `licenseGrade`
- `expiry_date` → verify > today + compare với `licenseExpiryDate`
- `full_name` → cross-check với CCCD name (phải khớp)

### 4.3 Giấy Đăng Ký Xe

```python
class VehicleRegistrationExtraction(BaseModel):
    license_plate: str          # Biển số xe
    brand: str                  # Nhãn hiệu
    model: str                  # Số loại/Model
    year_of_manufacture: int    # Năm sản xuất
    chassis_number: str         # Số khung
    engine_number: str          # Số máy
    registration_number: str    # Số đăng ký
    owner_name: str             # Tên chủ xe
    owner_id_number: str        # Số CCCD chủ xe (cross-verify)
    expiry_date: date           # Ngày hết hạn đăng ký
    confidence: float
    raw_text: str
```

**Fields dùng để verify:**
- `license_plate` → compare với `licensePlate`
- `brand` + `model` → compare với vehicle info
- `registration_number` → compare với `registrationNumber`
- `expiry_date` → verify > today + compare với `registrationExpiryDate`
- `owner_id_number` → phải khớp với CCCD id_number của driver (xe phải của driver hoặc chính chủ)

---

## 5. Verification Logic

### 5.1 Confidence Scoring

```python
class FieldMatchResult(BaseModel):
    field: str
    ocr_value: str
    submitted_value: str
    match_score: float      # 0.0–1.0 (fuzzy match ratio)
    is_critical: bool       # critical fields phải match ≥ 0.9

class DocumentMatchResult(BaseModel):
    document_type: str      # "cccd" | "license" | "vehicle_reg"
    confidence: float       # weighted average of field scores
    matched_fields: list[FieldMatchResult]
    critical_mismatch: bool # True nếu any critical field match < 0.9

class OverallVerificationResult(BaseModel):
    driver_id: UUID
    cccd_match: DocumentMatchResult
    license_match: DocumentMatchResult
    vehicle_reg_match: DocumentMatchResult
    overall_confidence: float  # weighted: cccd 40% + license 40% + vehicle 20%
    cross_checks_passed: bool  # name khớp cccd vs license, owner_id khớp cccd
    status: Literal["ocr_verified", "manual_review", "rejected"]
    rejection_reasons: list[str]
```

### 5.2 Quyết định tự động

| Condition | Status |
|---|---|
| `overall_confidence ≥ 0.85` và `cross_checks_passed = True` | `OcrVerified` → driver có thể hoạt động |
| `0.65 ≤ overall_confidence < 0.85` HOẶC `cross_checks_passed = False` | `ManualReview` → Admin xem xét |
| `overall_confidence < 0.65` HOẶC critical mismatch | `Rejected` → driver phải upload lại |
| GPLX hết hạn HOẶC B1/E grade | `Rejected` → lý do cụ thể |

### 5.3 Cross-checks bắt buộc

```
1. CCCD.full_name ~ License.full_name         (fuzzy ≥ 0.85)
2. CCCD.date_of_birth == License.date_of_birth
3. CCCD.id_number == VehicleReg.owner_id_number  (nếu có field này)
4. LicenseGrade compatible với VehicleType     (B2 không lái Truck3T)
5. License.expiry_date > today
6. VehicleReg.expiry_date > today
```

---

## 6. API Endpoints

```
POST /api/v1/ocr/extract/id-card          ← Phase A: auto-fill CCCD
POST /api/v1/ocr/extract/license          ← Phase A: auto-fill GPLX
POST /api/v1/ocr/extract/vehicle-reg      ← Phase A: auto-fill đăng ký xe
POST /api/v1/ocr/verify-driver            ← internal: full verification
GET  /health
GET  /ready
GET  /metrics
```

### Request/Response Examples

```http
POST /api/v1/ocr/extract/id-card
Authorization: Bearer <token>
{
  "front_url": "https://s3.../cccd_front_abc123.jpg",
  "back_url": "https://s3.../cccd_back_abc123.jpg"
}
```

```json
{
  "success": true,
  "data": {
    "id_number": "079123456789",
    "full_name": "NGUYEN VAN A",
    "date_of_birth": "1990-05-15",
    "gender": "Nam",
    "place_of_residence": "123 Nguyễn Trãi, Quận 1, TP.HCM",
    "expiry_date": "2035-05-15",
    "confidence": 0.93,
    "suggested_form_values": {
      "firstName": "Nguyễn",
      "lastName": "Văn A",
      "dateOfBirth": "1990-05-15",
      "address": "123 Nguyễn Trãi, Quận 1, TP.HCM"
    }
  }
}
```

> `suggested_form_values` là dữ liệu đã được normalize (title case, format chuẩn) — client dùng để pre-fill form.

```http
POST /api/v1/ocr/extract/license
Authorization: Bearer <token>
{
  "front_url": "https://s3.../gplx_front_abc123.jpg",
  "back_url": "https://s3.../gplx_back_abc123.jpg"
}
```

```json
{
  "success": true,
  "data": {
    "license_number": "079123456789",
    "full_name": "NGUYEN VAN A",
    "license_grade": "C",
    "expiry_date": "2028-12-31",
    "confidence": 0.91,
    "suggested_form_values": {
      "licenseNumber": "079123456789",
      "licenseGrade": "C",
      "licenseExpiryDate": "2028-12-31"
    }
  }
}
```

---

## 7. Kafka Integration

### Topics

| Topic | Direction | Producer | Consumer |
|---|---|---|---|
| `driver.documents.submitted` | → OCR | Driver service | OCR service |
| `ocr.driver.verification-completed` | → Driver | OCR service | Driver service |

### Event Schemas

```python
# Consumed:
class DriverDocumentsSubmittedEvent(BaseModel):
    message_id: UUID
    occurred_at: datetime
    schema_version: int = 1
    driver_id: UUID
    portrait_photo_url: str
    id_card_front_url: str
    id_card_back_url: str
    license_front_url: str
    license_back_url: str
    vehicle_reg_front_url: str
    vehicle_reg_back_url: str
    # Submitted values to compare against:
    submitted_full_name: str
    submitted_date_of_birth: str    # ISO date
    submitted_license_number: str
    submitted_license_grade: str
    submitted_license_expiry: str
    submitted_license_plate: str
    submitted_registration_number: str
```

```python
# Published:
class DriverVerificationCompletedEvent(BaseModel):
    message_id: UUID
    occurred_at: datetime
    schema_version: int = 1
    driver_id: UUID
    status: str                # "ocr_verified" | "manual_review" | "rejected"
    overall_confidence: float
    rejection_reasons: list[str]
    ocr_extracted_data: dict   # full extraction (for audit log)
```

---

## 8. Project Structure

```
src/Services/OCR/
  truck-delivery-ocr/
    src/
      ocr/
        __init__.py
        main.py                  ← FastAPI app, startup, routes register
        config.py                ← Settings (kafka, s3, otel)
        telemetry.py             ← OpenTelemetry setup
        models/
          __init__.py
          request.py             ← ExtractRequest, VerifyRequest
          response.py            ← CCCDExtraction, LicenseExtraction, ...
          events.py              ← Kafka event schemas
        routes/
          __init__.py
          extract.py             ← POST /extract/id-card, /license, /vehicle-reg
          health.py              ← GET /health, /ready, /metrics
        services/
          __init__.py
          image_loader.py        ← Download image from S3 URL → bytes
          id_card_ocr.py         ← CCCD/CMND OCR + parse
          license_ocr.py         ← GPLX OCR + parse
          vehicle_reg_ocr.py     ← Đăng ký xe OCR + parse
          verification.py        ← Compare OCR vs submitted, compute confidence
          name_normalizer.py     ← "NGUYEN VAN A" → "Nguyễn Văn A"
        consumers/
          __init__.py
          driver_documents_consumer.py  ← Kafka consumer (BackgroundTask)
    tests/
      __init__.py
      test_id_card_ocr.py
      test_license_ocr.py
      test_vehicle_reg_ocr.py
      test_verification.py
      fixtures/
        sample_cccd_front.jpg    ← Test fixture (redacted sample)
        sample_cccd_back.jpg
        sample_gplx_front.jpg
        sample_gplx_back.jpg
        sample_vehicle_reg.jpg
    pyproject.toml
    Dockerfile
    .env.example
    setup.sh
    run.sh
```

---

## 9. pyproject.toml (Reference)

```toml
[project]
name = "truck-delivery-ocr"
version = "0.1.0"
description = "Driver document OCR verification service — PaddleOCR + Vietnamese documents"
requires-python = ">=3.12"
dependencies = [
    "fastapi>=0.115",
    "uvicorn[standard]>=0.32",
    "pydantic>=2.9",
    "pydantic-settings>=2.6",
    "paddlepaddle>=3.0.0",              # CPU version
    "paddleocr>=2.9.1",
    "pillow>=11.0",
    "httpx>=0.27",                      # download images from S3
    "confluent-kafka>=2.6",             # Kafka consumer
    "opentelemetry-sdk>=1.28",
    "opentelemetry-exporter-otlp>=1.28",
    "opentelemetry-instrumentation-fastapi>=0.49b0",
    "structlog>=24.4",
    "fuzzywuzzy>=0.18",                 # fuzzy string matching for verify
    "python-Levenshtein>=0.21",         # fuzzywuzzy speedup
]

[project.optional-dependencies]
gpu = [
    "paddlepaddle-gpu>=3.0.0",          # Replace paddlepaddle nếu có GPU
]
dev = [
    "pytest>=8.3",
    "pytest-asyncio>=0.24",
    "httpx>=0.27",
]

[build-system]
requires = ["hatchling"]
build-backend = "hatchling.build"

[tool.hatch.build.targets.wheel]
packages = ["src/ocr"]

[tool.pytest.ini_options]
asyncio_mode = "auto"
testpaths = ["tests"]
```

---

## 10. Dockerfile

```dockerfile
FROM python:3.12-slim AS base
WORKDIR /app

# PaddleOCR cần một số system libs
RUN apt-get update && apt-get install -y \
    libgomp1 libglib2.0-0 libsm6 libxext6 libxrender-dev \
    && rm -rf /var/lib/apt/lists/*

FROM base AS deps
COPY pyproject.toml .
RUN pip install --no-cache-dir hatchling && \
    pip install --no-cache-dir .

FROM deps AS final
COPY src/ src/
EXPOSE 8090
CMD ["uvicorn", "ocr.main:app", "--host", "0.0.0.0", "--port", "8090"]
```

> ⚠️ PaddleOCR download pre-trained models on first run (~1GB). Cần mount volume hoặc bake models vào image trong production:
> ```dockerfile
> # Bake models vào image (production):
> RUN python -c "from paddleocr import PaddleOCR; PaddleOCR(use_angle_cls=True, lang='vi')"
> ```

---

## 11. Driver Service — Domain Changes

### 11.1 DriverVerificationStatus Enum (mới)

```csharp
// src/Services/Driver/TruckDelivery.Driver.Domain/ValueObjects/DriverVerificationStatus.cs
public enum DriverVerificationStatus
{
    Draft = 0,                    // Chưa nộp tài liệu
    PendingOcrVerification = 1,   // Tài liệu đã nộp, chờ OCR
    OcrVerified = 2,              // OCR xác nhận khớp (confidence ≥ 0.85)
    ManualReview = 3,             // Cần admin xem xét thủ công
    AdminVerified = 4,            // Admin xác nhận hợp lệ
    Rejected = 5                  // Từ chối (thông tin sai, tài liệu giả)
}
```

### 11.2 Driver Aggregate — Photo + Verification Fields

```csharp
// Thêm vào Driver aggregate:

// Document photos (URLs — stored in S3/MinIO)
public string PortraitPhotoUrl { get; private set; } = default!;
public string IdCardFrontUrl { get; private set; } = default!;
public string IdCardBackUrl { get; private set; } = default!;
public string LicenseFrontUrl { get; private set; } = default!;
public string LicenseBackUrl { get; private set; } = default!;
public string VehicleRegFrontUrl { get; private set; } = default!;
public string VehicleRegBackUrl { get; private set; } = default!;

// Verification
public DriverVerificationStatus VerificationStatus { get; private set; } = DriverVerificationStatus.Draft;
public float? OcrConfidenceScore { get; private set; }
public string? VerificationNotes { get; private set; }  // Rejection reason hoặc admin note
public string? IdCardNumber { get; private set; }       // Số CCCD — unique constraint
```

### 11.3 New Domain Methods

```csharp
// Sau khi documents uploaded — submit for verification
public Result SubmitDocuments(
    string portraitUrl,
    string idCardFrontUrl, string idCardBackUrl,
    string licenseFrontUrl, string licenseBackUrl,
    string vehicleRegFrontUrl, string vehicleRegBackUrl)
{
    // Validate all URLs not empty
    PortraitPhotoUrl = portraitUrl;
    IdCardFrontUrl = idCardFrontUrl;
    IdCardBackUrl = idCardBackUrl;
    LicenseFrontUrl = licenseFrontUrl;
    LicenseBackUrl = licenseBackUrl;
    VehicleRegFrontUrl = vehicleRegFrontUrl;
    VehicleRegBackUrl = vehicleRegBackUrl;
    VerificationStatus = DriverVerificationStatus.PendingOcrVerification;
    
    RaiseDomainEvent(new DriverDocumentsSubmittedDomainEvent(Id, /* all URLs */));
    return Result.Success();
}

// OCR service callback
public Result ApplyOcrResult(string status, float confidenceScore, string? notes)
{
    OcrConfidenceScore = confidenceScore;
    VerificationNotes = notes;
    VerificationStatus = status switch
    {
        "ocr_verified"   => DriverVerificationStatus.OcrVerified,
        "manual_review"  => DriverVerificationStatus.ManualReview,
        "rejected"       => DriverVerificationStatus.Rejected,
        _ => DriverVerificationStatus.ManualReview
    };
    
    RaiseDomainEvent(new DriverVerificationStatusChangedDomainEvent(Id, VerificationStatus));
    return Result.Success();
}

// Admin actions
public Result AdminVerify() { ... VerificationStatus = AdminVerified; ... }
public Result AdminReject(string reason) { ... VerificationStatus = Rejected; ... }

// Guard: phải Verified trước khi Available
public Result UpdateStatus(DriverStatus newStatus)
{
    if (newStatus == DriverStatus.Available &&
        VerificationStatus is not (DriverVerificationStatus.OcrVerified or DriverVerificationStatus.AdminVerified))
        return Result.Failure(Error.Conflict("Driver.Verification",
            "Tài xế chưa được xác minh. Vui lòng chờ hệ thống xác minh tài liệu."));
    // ...
}
```

### 11.4 New Admin Endpoints

```
GET  /api/v1/drivers?verificationStatus=ManualReview&page=  ← Admin review queue
POST /api/v1/drivers/{id}/verify                            ← Admin approve
POST /api/v1/drivers/{id}/reject                            ← Admin reject + reason
```

---

## 12. Kafka Events (Driver Service)

```csharp
// Published by Driver service:
public sealed record DriverDocumentsSubmittedEvent : IntegrationEvent
{
    public Guid DriverId { get; init; }
    public string PortraitPhotoUrl { get; init; } = default!;
    public string IdCardFrontUrl { get; init; } = default!;
    public string IdCardBackUrl { get; init; } = default!;
    public string LicenseFrontUrl { get; init; } = default!;
    public string LicenseBackUrl { get; init; } = default!;
    public string VehicleRegFrontUrl { get; init; } = default!;
    public string VehicleRegBackUrl { get; init; } = default!;
    // Submitted values for cross-verification:
    public string SubmittedFullName { get; init; } = default!;
    public string SubmittedDateOfBirth { get; init; } = default!;
    public string SubmittedLicenseNumber { get; init; } = default!;
    public string SubmittedLicenseGrade { get; init; } = default!;
    public string SubmittedLicenseExpiry { get; init; } = default!;
    public string SubmittedLicensePlate { get; init; } = default!;
    public string SubmittedRegistrationNumber { get; init; } = default!;
}

// Consumed by Driver service (from OCR):
public sealed record DriverOcrVerificationCompletedEvent : IntegrationEvent
{
    public Guid DriverId { get; init; }
    public string Status { get; init; } = default!;       // "ocr_verified" | "manual_review" | "rejected"
    public float OverallConfidence { get; init; }
    public string? RejectionReasons { get; init; }        // JSON array of strings
}
```

---

## 13. Photo Upload Flow

Driver app cần upload 7 ảnh trước khi gọi extract endpoints.

**Option A — Pre-signed S3 URL (recommended):**
```
GET /api/v1/uploads/presigned-url?type=driver-document&count=7
→ {
    urls: [
      { field: "portrait",      upload_url: "https://s3.../...", final_url: "..." },
      { field: "id_card_front", upload_url: "...", final_url: "..." },
      { field: "id_card_back",  upload_url: "...", final_url: "..." },
      { field: "license_front", upload_url: "...", final_url: "..." },
      { field: "license_back",  upload_url: "...", final_url: "..." },
      { field: "vehicle_reg_front", upload_url: "...", final_url: "..." },
      { field: "vehicle_reg_back",  upload_url: "...", final_url: "..." }
    ],
    expires_in: 3600
  }
```

Driver app upload trực tiếp lên S3, sau đó dùng `final_url` để gọi OCR extract.

**Option B — Multipart upload endpoint (simpler for MVP):**
```
POST /api/v1/drivers/documents/upload
Content-Type: multipart/form-data
  portrait: <file>
  id_card_front: <file>
  ...
→ { portrait_url, id_card_front_url, ... }
```

> Option B đơn giản hơn để implement. Option A scale tốt hơn (không qua backend).

---

## 14. Gateway Route

```json
// Thêm vào appsettings.json:
"ocr-route": {
  "ClusterId": "ocr-cluster",
  "AuthorizationPolicy": "default",
  "Match": { "Path": "/api/v1/ocr/{**catch-all}" }
},

"ocr-cluster": {
  "Destinations": {
    "primary": { "Address": "http://ocr-service:8090/" }
  }
}
```

> Extract endpoints cần Bearer token (driver phải đăng nhập trước khi upload/extract).
> Verify endpoint (`/verify-driver`) là internal — chỉ OCR service tự gọi qua Kafka, không expose trực tiếp.

---

## 15. Observability

- **Serilog** structured logging: `driver_id`, `document_type`, `confidence`, `ocr_duration_ms`
- **OpenTelemetry:** span cho mỗi OCR extraction + verification
- **Prometheus metrics:**
  - `ocr_extraction_duration_seconds{document_type}` — histogram
  - `ocr_verification_total{status}` — counter (ocr_verified / manual_review / rejected)
  - `ocr_confidence_score{document_type}` — histogram (phân phối confidence)

---

## 16. SLA / Performance

| Operation | Target | Ghi chú |
|---|---|---|
| Extract single document | < 3s | CPU inference |
| Extract single document (GPU) | < 0.5s | Production với GPU |
| Full async verify (3 docs) | < 10s | Kafka roundtrip included |
| Pre-signed URL generation | < 100ms | S3 API call |

> PaddleOCR cold start: ~5–10s lần đầu load model. Warm start: < 1s. Cần dùng startup event để pre-warm.

---

## 17. Security

- Ảnh giấy tờ chứa PII nhạy cảm → S3 bucket phải private, không public read
- Pre-signed URL TTL: 3600s (1 giờ) cho upload, 300s cho download (OCR service)
- Không log ảnh URL trong production logs (chỉ log `driver_id` và `document_type`)
- `IdCardNumber` (CCCD) phải unique trong DB — ngăn nhiều tài xế dùng chung CCCD

---

## 18. Tóm tắt Kafka Topics mới

| Topic | Format | Producer | Consumer |
|---|---|---|---|
| `driver.documents.submitted` | `{service}.{entity}.{action}` | Driver | OCR |
| `ocr.driver.verification-completed` | `{service}.{entity}.{action}` | OCR | Driver |
