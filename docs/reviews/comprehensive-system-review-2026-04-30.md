# Comprehensive System Review — Truck Delivery Backend
> **Ngày:** 2026-04-30 | **Phạm vi:** Toàn bộ 12 services (API Gateway → Analytics → OCR → Route → Optimizer)

---

## Executive Summary

Hệ thống truck delivery backend được thiết kế tốt với architecture tuân thủ chặt CQRS, event-driven patterns và microservice principles. Hầu hết services đã implement đúng Outbox pattern, Kafka consumers có idempotency và DLQ handling. Tuy nhiên có **2 vấn đề CRITICAL** và **3 vấn đề HIGH** cần xử lý trước production.

---

## 1. API Gateway

**Status:** ✅ ĐÚNG

### Đã làm đúng
- YARP reverse proxy cấu hình JWT Bearer authentication
- Rate limiting IP-based
- CorrelationIdMiddleware tạo và propagate `X-Correlation-Id`
- OpenTelemetry OTLP → Tempo
- `/health`, `/ready`, `/metrics` đầy đủ
- Routes đã cover: identity, order, driver, vehicles, shipment, tracking, notification, payment, analytics, ocr

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 2. Identity Service

**Status:** ✅ CƠ BẢN ĐÚNG — 1 vấn đề MEDIUM

### Đã làm đúng
- User aggregate với factory method + Result pattern
- Password validation (≥8 ký tự), email uniqueness guard
- JWT access token + refresh token flow
- Outbox pattern trong `RegisterUserCommandHandler`
- `PhoneNumber`, `DateOfBirth` đã có trong User aggregate
- Admin seed data (`AdminSeeder.cs`) và wiring trong `Program.cs`
- FluentValidation cho Commands
- OpenTelemetry, health checks, metrics đầy đủ

### Vấn đề

#### MEDIUM: Kafka Producer thiếu `EnableIdempotence`
- **File:** `src/Services/Identity/TruckDelivery.Identity.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- **Vấn đề:** `ProducerBuilder` được tạo mà không có `EnableIdempotence = true` và `Acks = Acks.Leader`. Identity service sử dụng Outbox pattern (không trực tiếp publish), nhưng nếu OutboxProcessor dùng IProducer này, message có thể bị duplicate nếu producer fail mid-send.
- **Fix:** Thêm `Acks = Acks.Leader, EnableIdempotence = true` vào `ProducerConfig`

#### MEDIUM: Database Seeding Blocking Startup
- **File:** `src/Services/Identity/TruckDelivery.Identity.Api/Program.cs`
- **Vấn đề:** Migration + admin seeding chạy synchronously tại startup, block app initialization
- **Recommendation:** Wrap trong `IHostedService` hoặc dùng async pattern

---

## 3. Order Service

**Status:** ✅ ĐÚNG

### Đã làm đúng
- Order aggregate với đầy đủ status transitions: Pending → Confirmed → AssignedToDriver → PickedUp → InTransit → Delivered → Completed(=8) → Cancelled
- `OrderItem` lưu dimensions (`LengthM`, `WidthM`, `HeightM`, `CanTilt`) cho bin-check
- 3 Kafka consumers đăng ký đúng: `OrderAssignedConsumer`, `ShipmentCompletedConsumer`, `PaymentCompletedConsumer`
- Idempotency check trên tất cả consumers
- DLQ routing trên tất cả consumers
- Outbox pattern đúng trong Command handlers
- `ShipmentId` field linking Order ↔ Shipment
- CQRS: Commands → EFCore, Queries → Dapper (không trộn lẫn)

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 4. Driver/Vehicle Service

**Status:** ✅ CƠ BẢN ĐÚNG — 1 vấn đề MEDIUM, 1 LOW

### Đã làm đúng
- Driver aggregate đầy đủ với 7 photo URLs, `DriverVerificationStatus`, `OcrConfidenceScore`
- `LicenseGrade` validation (block B1, E)
- `SelfRegisterDriverCommand`: tạo Driver + Vehicle trong một transaction
- Driver onboarding workflow 3 bước đầy đủ
- Admin verification/rejection endpoints (`AdminVerifyDriver`, `AdminRejectDriver`)
- `TrustScore` system (default 70, -3 khi breakdown, clamp 0-100)
- `BreakdownFraudGate`: check trust score ≥30, ≥1 photo, GPS Haversine ≤2km
- `FraudPatternAnalyzerJob`: chạy mỗi giờ, phát hiện pairs swap >3 lần
- `DriverSwapRecord` entity tracking reassignment history
- MinIO pre-signed URLs cho driver document upload
- `DriverOcrVerificationCompletedConsumer` với idempotency + DLQ
- Vehicle aggregate đầy đủ: `LengthM`, `WidthM`, `HeightM`, `RegistrationNumber`, `RegistrationExpiryDate`
- **Phase 2 mới thêm:** `GET /api/v1/drivers?status=&page=`, `GET /api/v1/vehicles?status=&page=`, `PUT /api/v1/vehicles/{id}/status`

### Vấn đề

#### MEDIUM: `IEventBus` Đăng ký Nhưng Không Dùng
- **File:** `src/Services/Driver/TruckDelivery.Driver.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- **Vấn đề:** `IEventBus` → `KafkaEventBus` đăng ký trong DI nhưng không inject vào bất kỳ handler nào. Tất cả events publish qua Outbox.
- **Impact:** Dead code trong DI container
- **Fix:** Xóa registration hoặc dùng `IEventBus` thực sự

#### LOW: License Expiry Check Timezone
- **File:** `src/Services/Driver/TruckDelivery.Driver.Domain/Aggregates/Driver.cs`
- **Vấn đề:** `licenseExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow)` — check tại UTC 00:00, có thể sai với timezone của user
- **Impact:** Biên check hết hạn có thể lệch ±1 ngày
- **Fix:** Dùng consistent UTC date comparison

---

## 5. Shipment Service

**Status:** ⚠️ CÓ VẤN ĐỀ — 2 HIGH, 1 MEDIUM

### Đã làm đúng
- Shipment aggregate với status machine đầy đủ (Created → Completed / Failed / Reassigning)
- `OrderCreatedConsumer` → `CreateShipmentCommand`
- `DriverAssignedConsumer` → bin-check → confirm/review dispatch
- `DispatchSagaOrchestrator` tự động poll + retry shipments
- `BreakdownSagaOrchestrator` xử lý breakdown reassignment (3 retries)
- Saga state persisted trong MongoDB
- `BreakdownReassignmentCompletedEvent` published (cho fraud analysis + escrow)
- `GET /api/v1/shipments` paginated với filters
- `POST /api/v1/shipments/{id}/decline-dispatch` (Admin)
- Idempotency trên consumers
- Outbox pattern đúng

### Vấn đề

#### HIGH: HTTP Bin-Check Trong Kafka Consumer
- **File:** `src/Services/Shipment/TruckDelivery.Shipment.Application/Consumers/DriverAssignedConsumer.cs`
- **Vấn đề:** `binCheckService.CheckAsync()` (HTTP POST tới Optimizer) được gọi trực tiếp bên trong Kafka consumer processing. Nếu HTTP call fail (transient error), consumer route thẳng vào DLQ thay vì retry via saga.
- **Impact:** Transient network errors → permanent failure. Bin-check không có retry logic.
- **Recommendation:** Chuyển bin-check vào saga step riêng (DispatchSagaOrchestrator) thay vì inline trong consumer

#### HIGH: `Shipment.Fail()` Không Raise Domain Event
- **File:** `src/Services/Shipment/TruckDelivery.Shipment.Infrastructure/Messaging/Kafka/Consumers/DispatchSagaOrchestrator.cs`
- **Vấn đề:** Khi saga hết retry, `shipment.Fail()` được gọi, nhưng `ShipmentFailedEvent` được thêm trực tiếp vào Outbox (bên ngoài aggregate method), bỏ qua domain event pattern.
- **Impact:** Domain event không được raise từ aggregate → vi phạm DDD. Observers không nhất quán.
- **Fix:** `Shipment.Fail()` phải raise `ShipmentFailedDomainEvent` internally, sau đó OutboxProcessor publish

#### MEDIUM: Route Service Hardcoded Placeholder
- **File:** `src/Services/Shipment/TruckDelivery.Shipment.Infrastructure/Messaging/Kafka/Consumers/DispatchSagaOrchestrator.cs`
- **Vấn đề:** Route planning step dùng hardcoded `50_000 meters, 3600 seconds` thay vì gọi Route service thực
- **Impact:** Tất cả shipments nhận fake route data; OR-Tools optimizer không nhận được dữ liệu thực tế
- **Fix:** Implement `routeClient.GetRouteAsync()` call thực sự tới Route Service (`:8084`)

---

## 6. Tracking Service

**Status:** ✅ ĐÚNG

### Đã làm đúng
- `ShipmentStartedConsumer` (topic: `shipment.shipment.started`) → `StartTrackingCommand`
- `ShipmentCompletedConsumer` → `StopTrackingCommand`
- `POST /api/v1/tracking/location` (Driver GPS push, every 1-5s)
- GPS location cached trong Redis `driver:gps:{driverId}` (TTL 10 min) → feed vào BreakdownFraudGate
- SignalR hub `/hubs/tracking` với JWT auth (query string cho WebSocket)
- Group-based broadcasting: `tracking:{shipmentId}`, `tracking:{driverId}`
- MongoDB persistence cho tracking points
- Outbox pattern cho location events
- Idempotency + DLQ trên consumers

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 7. Notification Service

**Status:** ✅ CƠ BẢN ĐÚNG — 1 LOW

### Đã làm đúng
- `DriverAssignedConsumer`, `ShipmentStatusUpdatedConsumer`, `PaymentCompletedConsumer` với idempotency + DLQ
- Status-aware notifications (InProgress → "picked up", Completed → "delivered")
- `DeviceToken` aggregate: upsert per userId+platform
- `POST /api/v1/notifications/register-device` (JWT auth, `RegisterDeviceCommand`)
- EFCore MySQL persistence (`Notifications`, `device_tokens`, `OutboxMessages` tables)
- JWT Bearer authentication đã được thêm vào Program.cs
- Migrations: `20260430000000_InitialCreate` + `20260430000001_AddDeviceTokens`

### Vấn đề

#### LOW: Stub Senders Luôn Thành Công
- **File:** `src/Services/Notification/TruckDelivery.Notification.Infrastructure/Notifications/`
- **Vấn đề:** StubPushSender/StubSmsSender/StubEmailSender chỉ log, luôn return success. Khi thay bằng real senders (FCM/Twilio/SMTP), DLQ flow chưa được test thực tế.
- **Recommendation:** Để comment rõ đây là stubs; trong test environment có thể inject mock senders fail ngẫu nhiên

---

## 8. Payment Service

**Status:** ✅ ĐÚNG

### Đã làm đúng
- `OrderDeliveredConsumer` (topic: `shipment.shipment.completed`) → `CreatePaymentCommand`
- COD flow: Payment auto-complete với `PaymentCompletedEvent` via Outbox
- `EscrowPayment` aggregate: Locked → Released/Disputed/Refunded
- `BreakdownReassignmentConsumer` → `CreateEscrowCommand` (50,000 VND surcharge)
- `ResolveEscrowCommand` xử lý confirm/dispute
- `GET /api/v1/payments/orders/{orderId}`, `GET /api/v1/payments/orders/{orderId}/escrow`
- **Phase 2 mới thêm:** `GET /api/v1/payments?status=&dateFrom=&dateTo=&page=` (Admin)
- Producer config đúng: `EnableIdempotence = true`, `Acks = Acks.Leader`
- Idempotency + DLQ trên consumers

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 9. Analytics Service

**Status:** ✅ ĐÚNG

### Đã làm đúng
- `VehicleBreakdownConsumer` → `RecordBreakdownCommand` → `BreakdownIncident`
- `BreakdownReassignmentCompletedConsumer` → tính `RecoveryTimeMinutes`, cập nhật `IsSuccessfullyReassigned`
- `SuspiciousDriverPairConsumer` → `RecordFraudAlertCommand` → `FraudAlert`
- KPI queries: breakdown count, reassignment success rate, avg recovery, fraud alerts
- Prometheus metrics: `analytics_reassignment_success_rate_pct`, `analytics_avg_recovery_time_minutes`, etc.
- `MetricsPublisherJob` chạy mỗi 1 phút
- Admin role required trên tất cả endpoints
- **Phase 2 mới thêm:** `POST /api/v1/analytics/fraud/alerts/{id}/acknowledge` → `AcknowledgeFraudAlertCommand`
- `IFraudAlertRepository` mở rộng với `GetByIdAsync` + `UpdateAsync`
- Idempotency + DLQ trên consumers
- MongoDB persistence

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 10. OCR Service (Python)

**Status:** 🔴 CÓ VẤN ĐỀ CRITICAL — 1 CRITICAL, 1 MEDIUM

### Đã làm đúng
- PaddleOCR models pre-warm khi startup (tránh cold start)
- OCR concurrent trên 6 ảnh trong một request
- Confidence scoring tổng hợp: CCCD 40% + License 40% + Vehicle 20%
- Auto-decision: ≥0.85 → `ocr_verified`, 0.65–0.85 → `manual_review`, <0.65 → `rejected`
- 6 cross-checks (name, DOB, owner_id, grade, license expiry, vehicle reg expiry)
- `DriverVerificationCompletedEvent` published với status + confidence
- DLQ handling khi OCR fail
- Prometheus metrics: `ocr_extraction_duration_seconds`, `ocr_extraction_total`, `ocr_verification_total`

### Vấn đề

#### CRITICAL: Thiếu Idempotency Check
- **File:** `src/Services/OCR/truck-delivery-ocr/src/ocr/consumers/driver_documents_consumer.py`
- **Vấn đề:** Comment có ghi "Idempotency: skip duplicates via message_id check would go here" nhưng KHÔNG ĐƯỢC IMPLEMENT. Consumer sẽ process duplicate `DriverDocumentsSubmittedEvent` messages.
- **Impact:**
  - Kafka re-deliver message khi timeout → OCR chạy 2 lần
  - Driver có thể nhận 2 kết quả verification khác nhau do race condition
  - Status có thể bị override từ `OcrVerified` → `Rejected` nếu message đến sau
- **Fix:** Implement Redis-backed idempotency (TTL 24h) check theo `message_id` trước khi process

#### MEDIUM: Threading Model Fragile
- **File:** `src/Services/OCR/truck-delivery-ocr/src/ocr/main.py`
- **Vấn đề:** Kafka consumer chạy trong daemon thread (`threading.Thread`), asyncio event loop managed thủ công. Nếu lifespan fail, daemon thread không cleanup đúng.
- **Impact:** Zombie Kafka consumer threads, message loss khi shutdown
- **Recommendation:** Wrap consumer trong proper async context manager hoặc `asyncio.create_task`

---

## 11. Route Service (Rust)

**Status:** ✅ ĐÚNG

### Đã làm đúng
- Axum async framework + tokio runtime (no blocking threads)
- A* pathfinding + Haversine fallback
- `/route`, `/matrix`, `/nearby-drivers`, `/drivers/:id/location` endpoints
- PostGIS spatial queries (road_network, driver_locations tables)
- Redis caching: route (30 min), matrix (15 min), nearby (1 min)
- Driver GPS cached trong Redis → feed BreakdownFraudGate
- OpenTelemetry OTLP → Tempo
- Prometheus `/metrics`
- `/health`, `/ready` endpoints

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 12. Optimizer Service (Python)

**Status:** ✅ ĐÚNG

### Đã làm đúng
- OR-Tools VRPPD solver (2N+1 node: depot + N pickup/delivery pairs)
- `AddPickupAndDelivery` enforce same-vehicle + pickup-before-delivery
- SLA-tier penalty scaling (express×3, standard×1, economy×0.5)
- Time window constraints via CumulVar khi có `time_matrix`
- LIFO constraints (O(n²), capped 30 orders)
- K-medoids geographic clustering (no coordinates needed, chỉ cần distance matrix)
- Greedy fallback khi OR-Tools timeout
- `POST /optimize`, `POST /bin-check` (3D: feasibility, diagonal, LIFO, priority)
- Stateless → horizontal scaling

### Vấn đề
Không có vấn đề được tìm thấy.

---

## 13. Cross-Service Kafka Issues

### HIGH: KafkaEventBus Topic Resolution Sai
- **File:** `src/Shared/TruckDelivery.Shared.Infrastructure/Messaging/Kafka/KafkaEventBus.cs`
- **Vấn đề:** `ResolveTopicName<TEvent>()` chuyển `DriverRegisteredEvent` → `driverregistered` thay vì `driver.driver.registered`. Consumers subscribe đúng topic nhưng IEventBus publish sai topic.
- **Impact:** Silent message loss nếu services dùng IEventBus trực tiếp. Hiện tại không bị vì tất cả đều dùng Outbox pattern với hardcoded topic names.
- **Recommendation:** Xóa `ResolveTopicName()` và require explicit topic trong từng publish call, hoặc dùng `[Topic("...")]` attribute

---

## Tổng Kết Vấn Đề Theo Priority

### CRITICAL (Cần Fix Ngay)
| # | Vấn đề | Service | File |
|---|---|---|---|
| C1 | OCR consumer không có idempotency check | OCR (Python) | `driver_documents_consumer.py` |
| C2 | `Shipment.Fail()` không raise domain event | Shipment | `DispatchSagaOrchestrator.cs` |

### HIGH (Fix Trước Production)
| # | Vấn đề | Service | File |
|---|---|---|---|
| H1 | Identity producer thiếu `EnableIdempotence` | Identity | `ServiceCollectionExtensions.cs` |
| H2 | Bin-check HTTP call trong Kafka consumer | Shipment | `DriverAssignedConsumer.cs` |
| H3 | KafkaEventBus topic resolution sai | Shared | `KafkaEventBus.cs` |

### MEDIUM (Sprint Tiếp Theo)
| # | Vấn đề | Service | File |
|---|---|---|---|
| M1 | Admin seeding blocking startup | Identity | `Program.cs` |
| M2 | IEventBus đăng ký nhưng không dùng | Driver | `ServiceCollectionExtensions.cs` |
| M3 | Route service dùng hardcoded placeholder | Shipment | `DispatchSagaOrchestrator.cs` |
| M4 | OCR consumer threading model fragile | OCR (Python) | `main.py` |

### LOW (Nice to Have)
| # | Vấn đề | Service | Chi tiết |
|---|---|---|---|
| L1 | License expiry check timezone edge case | Driver | UTC vs local date |
| L2 | Stub notification senders không test error path | Notification | StubPushSender, etc. |

---

## Điểm Tốt Nổi Bật

1. **Kafka Consumer Implementation xuất sắc** — Hầu hết consumers có idempotency, DLQ, OTel tracing đúng
2. **Outbox Pattern nhất quán** — Tất cả services đều publish events qua transactional outbox
3. **Domain Modeling chặt chẽ** — Aggregates đúng DDD, factory methods, guard clauses
4. **Verification Workflow toàn diện** — Driver onboarding 3 bước + OCR + Admin review
5. **Anti-fraud đa tầng** — TrustScore + BreakdownFraudGate + FraudPatternAnalyzerJob
6. **Polyglot đúng mục đích** — Rust (spatial), Python (optimization), .NET (business)
7. **Observable đầy đủ** — OpenTelemetry, Prometheus, Serilog, Grafana stack hoàn chỉnh
8. **Rate Limiting tại Gateway** — IP-based, bảo vệ toàn bộ hệ thống

---

## Khuyến Nghị Ưu Tiên

### Immediate (Làm Ngay)
1. Implement idempotency trong OCR consumer (`driver_documents_consumer.py`)
2. Thêm `EnableIdempotence = true` vào Identity Kafka producer
3. `Shipment.Fail()` phải raise `ShipmentFailedDomainEvent` internally

### Short-term (Sprint Tiếp)
1. Chuyển bin-check thành saga step async (tách khỏi Kafka consumer)
2. Xóa unused `IEventBus` registration khỏi Driver service
3. Implement Route service call thực trong `DispatchSagaOrchestrator`
4. Fix OCR consumer threading model

### Medium-term (2-3 Sprint)
1. Contract tests cho Kafka event schemas
2. Implement real notification senders (FCM, Twilio, SMTP)
3. Integration tests cho saga flows (success + rollback)
4. Payment gateway real integration (VNPay/card)
