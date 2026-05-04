# Truck Delivery Backend — Claude Rules

## Project Overview
Hệ thống giao hàng xe tải (truck delivery). Backend-only, greenfield, polyglot.
Solution file: `TruckDelivery.slnx` (16 .NET projects + 1 Rust crate).

## Languages & Responsibilities
- **.NET 10 (C#)** — primary, business logic, all microservices
- **Rust** — spatial queries (PostGIS), OpenStreetMap data processing
- **Python** — routing optimization (Google OR-Tools / VRP), ML tasks

## Databases
- **MySQL** — relational write DB (CQRS write side, via EFCore)
- **MongoDB** — document store (read projections, NoSQL use cases, Saga state)
- **PostGIS** — spatial/geo data (accessed only from Rust service)

## Infrastructure
- API Gateway: YARP
- Message broker: Apache Kafka (KRaft mode, 3 partitions/topic)
- Cache: Redis
- Logging: Serilog → Grafana Loki
- Monitoring: Prometheus
- Tracing: OpenTelemetry OTLP → Grafana Tempo
- Dashboard: Grafana
- Realtime: SignalR (WebSocket) — Tracking service (`/hubs/tracking`)

---

## Implementation Status

| Service | Technology | Port | Status | DB |
|---|---|---|---|---|
| API Gateway | .NET 10 / YARP | :8080 | ✅ Done | — |
| Identity | .NET 10 | :8081 | ✅ Done | MySQL (`truck_identity`) |
| Order | .NET 10 | :8082 | ✅ Done | MySQL (`truck_order`) |
| Driver/Vehicle | .NET 10 | :8083 | ✅ Done | MySQL (`truck_driver`) |
| Route | Rust / axum | :8084 | ✅ Done | PostGIS |
| Optimizer | Python / FastAPI | :8085 | ✅ Done | None |
| Shipment | .NET 10 | :8086 | ✅ Done | MySQL (`truck_shipment`) + MongoDB |
| Tracking | .NET 10 + SignalR | :8087 | ✅ Done | MongoDB (`truck_tracking`) |
| Notification | .NET 10 | :8088 | ✅ Done | MySQL (`truck_notification`) |
| Payment | .NET 10 | :8089 | ✅ Done | MySQL (`truck_payment`) |
| Analytics | .NET 10 | :8095 | ✅ Done | MongoDB (`truck_analytics`) |
| OCR | Python / FastAPI | :8090 | ✅ Done | None (stateless) |

### What's Done in Completed Services
- **Identity:** RegisterUser, Login, RefreshToken commands; JWT service; EFCore User aggregate; `UserRegisteredEvent` → Kafka topic `userregistered`
- **Driver:** RegisterDriver, RegisterVehicle, AssignVehicleToDriver, UpdateDriverStatus commands; GetDriverById, GetVehicleById, ListAvailableDrivers queries; `UserRegisteredConsumer` (consumes `userregistered`); publishes `DriverRegisteredEvent`, `DriverStatusChangedEvent`, `VehicleAssignedToDriverEvent`; **Phase 1 additions:** `LicenseGrade` enum (B1/B2/C/D/E/FC/FD); `DriverVerificationStatus` enum (Draft/PendingOcrVerification/OcrVerified/ManualReview/AdminVerified/Rejected); Driver aggregate updated with `DateOfBirth`, `Address`, `IdCardNumber`, `LicenseGrade`, `LicenseExpiryDate`, 7 photo URL fields, verification fields, `SubmitDocuments()`, `ApplyOcrResult()`, `AdminVerify()`, `AdminReject()` methods; UpdateStatus guard prevents Available before OcrVerified/AdminVerified; Vehicle aggregate updated with `LengthM`, `WidthM`, `HeightM`, `RegistrationNumber`, `RegistrationExpiryDate`; EFCore configurations updated; migration `20260430000000_AddDriverVerificationAndVehicleDimensions` created; **Phase 2 additions:** `POST /api/v1/drivers/register` → `SelfRegisterDriverCommand` (creates Driver + Vehicle in one tx, transitions to PendingOcrVerification, publishes `DriverDocumentsSubmittedEvent` → topic `driver.documents.submitted`); `DriverOcrVerificationCompletedConsumer` (consumes `ocr.driver.verification-completed` → `ApplyOcrResultCommand` → maps string status to enum, updates Driver aggregate); `GET /api/v1/drivers/pending-verification` (Admin — lists drivers in PendingOcrVerification|ManualReview); `POST /api/v1/drivers/{id}/verify` (Admin → `AdminVerifyDriverCommand`); `POST /api/v1/drivers/{id}/reject-verification` (Admin → `AdminRejectDriverCommand`); `GET /api/v1/uploads/presigned-url?type=driver-document` (Driver role → MinIO pre-signed PUT URLs for 7 document photos); MinIOStorageService + IStorageService interface; Minio SDK added to Driver.Infrastructure
- **Order:** CreateOrder, CancelOrder commands; GetOrderById, ListOrdersByCustomer queries; publishes `OrderCreatedEvent` → topic `order.order.created`, `OrderCancelledEvent`; `OrderItem` có `LengthM`, `WidthM`, `HeightM`, `CanTilt` (nullable) cho bin-check; `OrderCreatedEvent` carries `Items` list với dimensions; **Phase 1 additions:** `ShipmentId` field (nullable Guid, links Order to its Shipment); `SetShipmentId()` method; `OrderStatus.Completed=8` added; 3 Kafka consumers: `OrderAssignedConsumer` (topic `shipment.driver.assigned` → `AssignedToDriver` + sets ShipmentId), `ShipmentCompletedConsumer` (topic `shipment.shipment.completed` → `Delivered`), `PaymentCompletedConsumer` (topic `payment.payment.completed` → `Completed`); `UpdateOrderStatusCommand` + Handler; `SetOrderShipmentCommand` + Handler; migration `20260430000000_AddShipmentIdToOrder` created
- **Route (Rust):** A\* pathfinding, Haversine fallback; `/route`, `/matrix`, `/nearby-drivers`, `/drivers/:id/location` endpoints; PostGIS migrations (`driver_locations`, `road_network` tables); Redis cache (route 30 min, matrix 15 min, nearby 1 min); OpenTelemetry → Tempo; `setup.ps1/sh` + `run.ps1/sh`
- **Optimizer (Python):** OR-Tools VRPPD solver (2N+1 node model: depot + pickup/delivery pairs); `AddPickupAndDelivery` enforces same-vehicle + pickup-before-delivery; per-node CumulVar time windows when `time_matrix` provided; SLA-tier penalty scaling (express×3, standard×1, economy×0.5); `POST /optimize`; `POST /bin-check` (3D bin packing: feasibility, diagonal placement, LIFO check, priority scoring); `OrderInfo` fields: `earliest_pickup_unix`, `hard_deadline_unix`, `desired_delivery_unix`, `sla_tier`; JSON serialized in snake_case from C# (`JsonNamingPolicy.SnakeCaseLower`); **Phase 4 — LIFO + Clustering:** `enable_lifo` flag on `OptimizeRequest`; O(n²) pairwise LIFO constraints via `IsEqualVar`/`IsLessOrEqualVar` on CumulVar (requires `time_matrix`, capped at 30 orders); LIFO-aware greedy fallback (all pickups in reverse-delivery order → all deliveries in delivery order); K-medoids geographic clustering via farthest-first init on distance matrix (no coordinates needed) — assigns geographically close orders to the same vehicle cluster; strategy reported as `vrp-lifo`/`greedy-lifo` when LIFO enabled
- **Shipment:** `OrderCreatedConsumer` → `CreateShipmentCommand` (stores packages JSON on shipment); `DispatchSagaOrchestrator` (polls Created/RoutePlanning → publishes `DriverAssignmentRequestedEvent`); `DriverAssignedConsumer` → calls `/bin-check` (if vehicle + package dimensions available) → if `requires_dispatcher_confirmation=true` → `DispatcherReviewRequired` status + publishes `DispatcherConfirmationRequiredEvent`; otherwise → `InProgress` + `ShipmentStartedEvent`; `POST /api/v1/shipments/{id}/confirm-dispatch` (Admin) → `ConfirmDispatchCommand` → InProgress + ShipmentStartedEvent; `ShipmentStatus` enum: `DispatcherReviewRequired=8`; `IBinCheckService` interface in Application layer; MongoDB saga state; EFCore MySQL write + Dapper read; **Phase 1 additions:** `GET /api/v1/shipments` (paginated list with filters: `status`, `customerId`, `driverId`, `orderId`); `POST /api/v1/shipments/{id}/decline-dispatch` (Admin, uses `FailShipmentCommand` → Shipment `Failed` + publishes `ShipmentFailedEvent`); `PagedResult<T>` DTO
- **Tracking:** `ShipmentStartedConsumer` → `StartTrackingCommand`; `ShipmentCompletedConsumer` → `StopTrackingCommand`; `POST /api/v1/tracking/location` (driver GPS push); `GET /api/v1/tracking/shipments/{id}/points`; SignalR hub at `/hubs/tracking` (groups by shipment/driver); MongoDB persistence; Mongo Outbox processor
- **Notification:** Consumes `ShipmentStatusUpdatedEvent`, `DriverAssignedEvent`, `PaymentCompletedEvent`; `SendNotificationCommand` → Push/SMS/Email senders; EFCore MySQL `NotificationRecord` persistence; Outbox pattern; all consumers use `ConsumerConfig` + own `IConsumer` (thread-safe); **Phase 2 additions:** `POST /api/v1/notifications/register-device` → `RegisterDeviceCommand` → upsert `DeviceToken` (one token per userId+platform); `DeviceToken` aggregate + `IDeviceTokenStore` interface + `DeviceTokenStore` EFCore impl; `DeviceTokenConfiguration` (table `device_tokens`, unique index on UserId+Platform); JWT auth added to Notification API; migration `20260430000001_AddDeviceTokens` created; **Sprint 2 additions:** `DriverManualReviewConsumer` (topic `driver.driver.manual-review-required` → push + email to admin); `FcmPushSender` (real Firebase FCM using `FirebaseAdmin` SDK, `IDeviceTokenStore.GetTokensByUserIdAsync()`); conditional DI: `FcmPushSender` if `Firebase:CredentialsJson` configured else `StubPushSender`; `NotificationType.DriverManualReviewRequired=7`
- **Payment:** `OrderDeliveredConsumer` → `CreatePaymentCommand` (COD flow: auto-complete); publishes `PaymentCompletedEvent` via Outbox; `GET /api/v1/payments/orders/{orderId}`; EFCore MySQL + Dapper read; `PaymentStatus` state machine; **Phase 1 additions:** `GET /api/v1/payments/orders/{orderId}/escrow` → returns `EscrowDto`; `EscrowDto` DTO; **Sprint 2 additions:** `PaymentMethod` enum (Cod=1, VnPay=2) in `Domain/ValueObjects/`; `Payment.Method` property; `IPaymentGateway` + `IPaymentGatewayFactory` interfaces; `VnPayGateway` (HMAC-SHA512), `CodGateway`, `PaymentGatewayFactory`; `InitiatePaymentCommand` + Handler → `POST /api/v1/payments/orders/{orderId}/initiate` (Customer); `HandleVnPayCallbackCommand` + Handler; `WebhookController` → `GET|POST /api/v1/payments/webhook/vnpay`; migration `20260501000000_AddPaymentMethodToPayments`; VNPay config in `appsettings.json`
- **OCR Service (:8090, stateless Python):** Vietnamese document OCR verification; PaddleOCR (lang='vi') singleton per document type; 3 extraction endpoints: `POST /api/v1/ocr/extract/id-card`, `/extract/license`, `/extract/vehicle-reg`; Kafka consumer `DriverDocumentsSubmittedEvent` (topic `driver.documents.submitted`) → concurrent OCR on 6 images → `compute_overall_verification` → publishes `DriverVerificationCompletedEvent` (topic `ocr.driver.verification-completed`); confidence scoring: CCCD 40% + license 40% + vehicle 20%; auto-decision: ≥0.85 → `ocr_verified`, 0.65–0.85 → `manual_review`, <0.65 → `rejected`; 6 cross-checks (name match, DOB match, owner_id match, grade validity, license expiry, vehicle reg expiry); Prometheus metrics: `ocr_extraction_duration_seconds{document_type}`, `ocr_extraction_total{document_type,status}`, `ocr_verification_total{status}`; DLQ on failure
- **Phase 7 — Full Analytics Dashboard:**
  - **Analytics Service (:8095, MongoDB `truck_analytics`):** Dedicated analytics microservice; consumes 3 Kafka topics; stores in MongoDB; exposes REST API; publishes Prometheus metrics for Grafana
  - **Kafka Consumers:** `VehicleBreakdownConsumer` (topic `driver.vehicle.breakdown`) → `RecordBreakdownCommand`; `BreakdownReassignmentCompletedConsumer` (topic `shipment.breakdown.reassignment-completed`) → `RecordReassignmentCompletedCommand` (resolves & calculates recovery time); `SuspiciousDriverPairConsumer` (topic `driver.fraud.suspicious-pair-detected`) → `RecordFraudAlertCommand`
  - **Domain Documents:** `BreakdownIncident` (tracks lifecycle: reported → resolved, IsSuccessfullyReassigned, RecoveryTimeMinutes); `FraudAlert` (records collusion detections)
  - **KPI Queries:** `GET /api/v1/analytics/kpis?days=30` → breakdown count, reassignment success rate, avg recovery time, fraud alerts; `GET /api/v1/analytics/breakdown/incidents` → incident list; `GET /api/v1/analytics/fraud/alerts` → fraud alert list; all endpoints require Admin role
  - **Prometheus Metrics:** `MetricsPublisherJob` (1-min background job) publishes `analytics_reassignment_success_rate_pct` gauge, `analytics_avg_recovery_time_minutes` gauge, `analytics_breakdown_incidents_total{risk_level}` counter, `analytics_fraud_alerts_total` counter → consumed by Grafana dashboards via `/metrics` endpoint
- **Phase 5 — Breakdown Saga + Anti-Fraud Gate:**
  - **Driver Service:** `TrustScore` property on `Driver` aggregate (default 70, range 0–100); `ReportBreakdown(lat,lng,riskLevel)` → deducts -3 trust score, sets status Offline, marks vehicle `Breakdown`; `VehicleStatus.Breakdown=4` added; `BreakdownReport` entity (stores photos, GPS, risk level); `POST /api/v1/drivers/{id}/report-breakdown` (Driver role)
  - **Anti-Fraud Gate (`IBreakdownFraudGate`):** Validates: trust score ≥ 30, ≥1 photo required, GPS Haversine distance check vs Redis cache `driver:gps:{driverId}` (≤2km = Low risk, >2km = Medium risk); gate rejection returns 422
  - **VehicleBreakdownEvent** → Kafka topic `driver.vehicle.breakdown`; `FraudRiskLevel` enum: Unknown/Low/Medium/High/Confirmed
  - **Shipment Service:** `ShipmentStatus.Reassigning=9`; `MarkReassigning(reason)` clears driver/vehicle assignment; `GetActiveByDriverIdAsync` on IShipmentRepository; `VehicleBreakdownConsumer` consumes `driver.vehicle.breakdown` → `HandleVehicleBreakdownCommand` → finds InProgress shipment for broken driver → marks Reassigning
  - **BreakdownSagaOrchestrator:** Polls `Reassigning` shipments every 10s; transitions `Reassigning → DriverAssigning` + publishes `DriverAssignmentRequestedEvent` to re-enter existing dispatch pipeline; max 3 retries then `Failed`; `BreakdownSagaState` persisted in MongoDB collection `breakdown_saga_states`
  - **Tracking Service:** GPS location updates now cache `driver:gps:{driverId}` in Redis (TTL 10min) via `IDriverGpsCache` / `RedisDriverGpsCache`; feeds breakdown fraud gate
- **Phase 6 — Fraud Network Analysis + Escrow Payment:**
  - **Driver Service — Collusion Detection:** `DriverSwapRecord` entity (MySQL, `driver.DriverSwapRecords` table) tracks (originalDriverId, replacementDriverId, shipmentId) per reassignment; `IDriverSwapRecordRepository` + `DriverSwapRecordRepository`; `BreakdownReassignmentConsumer` consumes topic `shipment.breakdown.reassignment-completed` → `RecordDriverSwapCommand` → persists swap pair
  - **Driver Service — FraudPatternAnalyzerJob:** Background service runs hourly; groups `DriverSwapRecords` by pair, detects count > 3 → penalises both drivers' TrustScore (-10 each) + publishes `SuspiciousDriverPairDetectedEvent` → topic `driver.fraud.suspicious-pair-detected` via Outbox
  - **Shipment Service:** Added `OriginalBreakdownDriverId` + `IsBreakdownReassignment` fields to `Shipment` aggregate; `MarkReassigning()` captures original driver before clearing; `DriverAssignedConsumer` publishes `BreakdownReassignmentCompletedEvent` → topic `shipment.breakdown.reassignment-completed` when a reassignment completes
  - **Payment Service — Escrow:** `EscrowPayment` aggregate (states: Locked/Released/Disputed/Refunded); `EscrowStatus` enum; `IEscrowPaymentRepository`; `BreakdownReassignmentConsumer` in Payment service consumes `shipment.breakdown.reassignment-completed` → `CreateEscrowCommand` → locks 50,000 VND surcharge fee; `ResolveEscrowCommand` handles confirm/dispute; `POST /api/v1/payments/escrow/{id}/confirm` + `POST /api/v1/payments/escrow/{id}/dispute` (Customer/Admin role)

### Phase 2 Additions (List APIs + Pagination)
- **Shared.Common:** `PagedResult<T>` added to `Primitives/` — used across all services for paginated responses
- **Driver:** `GET /api/v1/drivers?status=&page=` (Admin, Dapper, `ListDriversQuery`); `GET /api/v1/vehicles?status=&driverId=&type=&page=` (Admin, Dapper, `ListVehiclesQuery`); `PUT /api/v1/vehicles/{id}/status` (Admin, EFCore, `UpdateVehicleStatusCommand` — supports Available/Maintenance)
- **Payment:** `GET /api/v1/payments?status=&dateFrom=&dateTo=&page=` (Admin, Dapper, `ListPaymentsQuery`)
- **Analytics:** `POST /api/v1/analytics/fraud/alerts/{id}/acknowledge` (Admin, `AcknowledgeFraudAlertCommand`); `IFraudAlertRepository` extended with `GetByIdAsync` + `UpdateAsync`

### Issue Fixes (2026-04-30 comprehensive review)
- **[C2-FIXED] Shipment.Fail() domain event bug:** Captured `previousStatus` before setting `Status = Failed` so `ShipmentStatusChangedDomainEvent` carries correct old/new statuses
- **[H1-FIXED] Identity Kafka producer idempotency:** Added `Acks = Acks.Leader, EnableIdempotence = true` to `ProducerConfig` in `Identity.Infrastructure`
- **[H2-FIXED] DriverAssignedConsumer bin-check retry:** Extracted `BinCheckWithRetryAsync()` (3 attempts, 2s×n backoff) so transient HTTP failures don't immediately DLQ the message
- **[H3-FIXED] KafkaEventBus broken topic resolution:** `IEventBus.PublishAsync()` now requires explicit `string topic` parameter; `ResolveTopicName<TEvent>()` helper removed
- **[C1-FIXED] OCR Redis idempotency:** Added `redis>=5.0` dep, `redis_url` config, `RedisIdempotencyStore` module; `DriverDocumentsConsumer` now checks + marks processed per `message_id`
- **[M4-FIXED] OCR threading:** Consumer thread is non-daemon (`daemon=False`) + `_consumer_thread.join(timeout=10)` on shutdown for clean drain
- **[M1-FIXED] Identity startup blocking:** Migration + seeding moved from `Program.cs` to `DatabaseInitializerService : IHostedService`; app now accepts health-check requests immediately
- **[M2-FIXED] Driver unused IEventBus:** Removed `services.AddScoped<IEventBus, KafkaEventBus>()` and unused `using` imports from `Driver.Infrastructure`
- **[L1-FIXED] Driver license expiry off-by-one:** Guard changed from `<=` to `<` so a license expiring today is still accepted
- **[L2-FIXED] Notification stub TODO comments:** Added `// TODO: Replace with real FCM/Twilio/SMTP…` comment to each stub sender

### Sprint 1 Fixes (2026-05-01)
- **[G-S1-FIXED] Driver status role guard:** `PUT /drivers/{id}/status` now requires `Admin,Driver` role. Driver role can only update own status (RequestingUserId == DriverId check in handler). `Error.Forbidden` added to `Shared.Common.Primitives.Error`.
- **[G-S2-FIXED] Shipment status restriction:** `PUT /shipments/{id}/status` guards Driver role to only `PickedUp`, `InTransit`, `Delivered` — Forbid() returned for any other status.
- **[G-B1-FIXED] LicenseGrade filter in Optimizer:** `DriverInfo.license_grade` + `OptimizeRequest.required_license_grades` added to Python models; filter applied before VRP solving in `/optimize` route. C# DTOs updated (`DriverInfo.LicenseGrade`, `OptimizeRequest.RequiredLicenseGrades`). `DriverAssignmentRequestedEvent` extended with `RequiredLicenseGrades`.
- **[G-B5-FIXED] SignalR DriverAssigned:** `ITrackingNotifier.NotifyDriverAssignedAsync()` added and implemented in `TrackingHubNotifier` (sends `DriverAssigned` event to `driver:{driverId}` SignalR group). `ShipmentStartedConsumer` emits after tracking starts.
- **[G-B6-FIXED] Breakdown photo presigned URL:** `GET /api/v1/uploads/presigned-url?type=breakdown-photo&count=N` (1–10). `IStorageService.GenerateBreakdownPhotoUrlsAsync()` added; `MinIOStorageService` uses `breakdown-photos` bucket. `MinIOStorageService` refactored to shared `GeneratePresignedUrlsAsync`.
- **[G-B10-FIXED] OrderDto.ShipmentId:** `ShipmentId: Guid?` added to `OrderDto` and `OrderSummaryDto`. Both `GetOrderByIdQueryHandler` and `ListOrdersByCustomerQueryHandler` Dapper queries now select `o.ShipmentId`.
- **[G-S4-FIXED] Coordinate range validation:** `CreateOrderCommandValidator` validates `Latitude ∈ [-90,90]` and `Longitude ∈ [-180,180]` for both pickup and delivery addresses (nullable, conditional).
- **[G-S3-FIXED] IdCardNumber duplicate check:** `IDriverRepository.ExistsByIdCardNumberAsync()` added (interface + EFCore impl). `SelfRegisterDriverCommandHandler` checks before creating driver — returns `Error.Conflict("Driver.IdCard", ...)` with proper 409 response.

### Sprint 2 Fixes (2026-05-01)
- **[G-B7-FIXED] TrustScore + VerificationStatus + LicenseGrade in DriverDto:** `DriverDto` updated with `VerificationStatus`, `LicenseGrade`, `TrustScore` fields. `GetDriverByIdQueryHandler` Dapper SQL updated to select these columns.
- **[G-B8-FIXED] Order pagination with date/status filter:** `ListOrdersByCustomerQuery` updated with `Status`, `DateFrom`, `DateTo` filter params; returns `PagedResult<OrderSummaryDto>` using `COUNT(*) OVER()` window function. `OrdersController` updated with corresponding query params.
- **[G-B4-FIXED] Admin notification when ManualReview:** `DriverManualReviewRequiredEvent` published from `ApplyOcrResultCommandHandler` via Outbox (topic `driver.driver.manual-review-required`) when OCR returns ManualReview. `DriverManualReviewConsumer` in Notification service sends push + email to admin (`Notification:AdminEmail` config). `NotificationType.DriverManualReviewRequired=7` added.
- **[G-B2-FIXED] Firebase FCM real push sender:** `FcmPushSender` implements `IPushNotificationSender` using `FirebaseAdmin` SDK. Looks up device tokens via `IDeviceTokenStore.GetTokensByUserIdAsync()`. Conditionally registered — falls back to `StubPushSender` if `Firebase:CredentialsJson` not configured. `FirebaseAdmin` NuGet added to `Notification.Infrastructure.csproj`.
- **[G-B3-FIXED] VNPay payment gateway integration:** `PaymentMethod` enum (Cod=1, VnPay=2) added to `Payment.Domain/ValueObjects/`. `Payment` aggregate updated with `Method` property. `IPaymentGateway` + `IPaymentGatewayFactory` interfaces in Application layer. `VnPayGateway` (HMAC-SHA512 URL generation + callback verify) + `CodGateway` + `PaymentGatewayFactory` in Infrastructure/Gateways. `InitiatePaymentCommand` + Handler: `POST /api/v1/payments/orders/{orderId}/initiate` (Customer role, returns `paymentId` + `paymentUrl`). `HandleVnPayCallbackCommand` + Handler: verifies signature, authorizes + completes payment, publishes `PaymentCompletedEvent`/`PaymentFailedEvent` via Outbox. `WebhookController`: `GET|POST /api/v1/payments/webhook/vnpay`. EF migration `20260501000000_AddPaymentMethodToPayments` created. VNPay config in `appsettings.json` (`VnPay:TmnCode`, `VnPay:HashSecret`, `VnPay:PaymentUrl`, `VnPay:ReturnUrl`).
- **[G-B9-FIXED] Admin accounts endpoint:** `POST /api/v1/admin/accounts` already existed in `Identity.Api/Controllers/AdminController.cs` — verified no additional work needed.

### Sprint 3 Fixes (2026-05-01)
- **[G-T5-FIXED] GPS rate limit per-user:** `UserIdInjectionMiddleware` created in Gateway — reads JWT `sub` claim after `UseAuthentication()` and injects `X-User-Id` header; `IpRateLimiting` config updated: `ClientIdHeader = "X-User-Id"`, `EnableEndpointRateLimiting = true`, endpoint rule `POST:/api/v1/tracking/location → 120 req/min per user` (vs 300 general per IP).
- **[G-T6-FIXED] Health check aggregate:** Gateway `Program.cs` registers `AddUrlGroup` health checks for all 8 downstream services (identity, order, driver, shipment, tracking, notification, payment, analytics); exposed `GET /health/all` with JSON aggregate response; `GET /ready` now polls all downstream services. Package `AspNetCore.HealthChecks.Uris v8.0.1` added to Gateway.
- **[G-T3-FIXED] integration.yml GitHub Actions:** `.github/workflows/integration.yml` created — runs integration tests (Order + Shipment) + contract tests on push/PR to develop/main/master; timeout 10 min per integration test project.
- **[G-T1-FIXED] Integration tests:** `tests/Integration/TruckDelivery.Order.Application.IntegrationTests/` — 3 test classes (OrderRepositoryTests, OrderIdempotencyTests, CreateOrderCommandTests) using Testcontainers MySQL + Redis; `tests/Integration/TruckDelivery.Shipment.Application.IntegrationTests/` — 2 test classes (ShipmentRepositoryTests, SagaRepositoryTests) using Testcontainers MySQL + MongoDB; both use `IClassFixture` + `IAsyncLifetime` pattern.
- **[G-T2-FIXED] Contract tests:** `tests/Contract/TruckDelivery.Contracts.Tests/EventSchemaTests.cs` — validates 6 Kafka event schemas: `OrderCreatedEvent`, `DriverAssignmentRequestedEvent`, `ShipmentCompletedEvent`, `ShipmentStartedEvent`, `PaymentCompletedEvent`, `DriverAssignedEvent`; tests include envelope contract (MessageId/OccurredAt/SchemaVersion), JSON round-trip, and forward-compatibility (extra fields ignored).

### Sprint 4 Fixes (2026-05-01)
- **[G-S5-VERIFIED] Refresh token rotation:** `RefreshTokenCommandHandler` already implements rotation — `SetRefreshToken()` overwrites old token atomically before `SaveChangesAsync`; old token immediately invalid.
- **[G-T4-VERIFIED] OCR Docker model bake:** `Dockerfile` already pre-downloads PaddleOCR Vietnamese models in builder stage (`python -c "from paddleocr import PaddleOCR; PaddleOCR(...)"`) + copies to runtime image. No first-run download needed.
- **[PhoneNumber/DOB-VERIFIED] Customer fields:** `PhoneNumber` and `DateOfBirth` already in `User` aggregate and `RegisterUserCommand` as of prior sprint.
- **[G-S6-FIXED] Analytics gateway defense-in-depth:** `AdminOnly` authorization policy registered in Gateway `Program.cs` (`opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"))`); `analytics-route` in `appsettings.json` changed from `"default"` to `"AdminOnly"` — analytics blocked at Gateway for non-Admin even if service is reached directly.
- **[G-SMS-FIXED] Twilio SMS real sender:** `TwilioSmsSender` created in `Notification.Infrastructure/Notifications/`; uses `Twilio v7.4.1` SDK; conditionally registered when `Twilio:AccountSid` configured (falls back to `StubSmsSender`); `Twilio` NuGet added to `Notification.Infrastructure.csproj`.
- **[G-EMAIL-FIXED] SMTP Email real sender:** `SmtpEmailSender` created using `MailKit v4.9.0`; sends HTML email via `StartTls`; conditionally registered when `Smtp:Host` configured (falls back to `StubEmailSender`); `MailKit` NuGet added; `Smtp:*` config section added to `Notification.Api/appsettings.json`.

### Doc Sprint (2026-05-01)
- **[G-D3-FIXED] Admin Portal guide:** `docs/mobile-integration/03-admin-portal.md` — Auth flow, shipment management, driver verification queue, analytics endpoints, escrow operations, SignalR alerts, polling strategy, error handling.
- **[G-D4-FIXED] Database schema:** `docs/architecture-business/02-domain/database-schema.md` — All MySQL databases (6), MongoDB collections (truck_tracking, truck_analytics, saga states), PostGIS tables; column-level detail for key tables.
- **[G-D5-FIXED] api-reference.md Phase 2+ endpoints:** Appended full section: driver self-register, verification endpoints, vehicle CRUD, presigned upload URLs, shipment admin ops, payment VNPay/escrow, notification register-device, analytics KPIs, admin accounts.
- **[G-D6-FIXED] Production deployment guide:** `docs/deployment/01-production-setup.md` — Env vars per service, DB migration commands, Kafka topic creation script, Docker Compose, OCR cold start, health check verification, Grafana/Prometheus/Loki/Tempo setup, admin seeding.
- **[G-D7-FIXED] MinIO setup guide:** `docs/deployment/02-minio-setup.md` — Bucket creation, IAM policies, CORS config, pre-signed URL flow (TTLs), retention policy, production recommendations.
- **[G-D1-FIXED] api-gap-analysis.md:** Updated header + summary table to reflect Sprint 1–4 complete (21/21 gaps done).

### All Gaps Resolved
All 28 items from the 2026-05-01 upgrade proposal are complete: Sprint 1 (8) + Sprint 2 (6) + Sprint 3 (5) + Sprint 4 (4) + Doc Sprint (7) = 28/28 ✅
- **[M3-FIXED] DispatchSagaOrchestrator real Route Service call:** `AddressRequest` now accepts `Latitude?`/`Longitude?`; `Order` aggregate stores 4 coordinate fields; `OrderCreatedEvent` (both publisher + consumer-side DTOs) carries coordinates; `Shipment` aggregate stores pickup/delivery coordinates; `DispatchSagaOrchestrator` calls `routeClient.GetRouteAsync()` when coords present, falls back to Haversine when Route Service is unreachable, and falls back to static placeholder when coords are absent; migrations `20260430000001_AddCoordinatesToOrder` + `20260430000001_AddCoordinatesToShipment` created.

### E2E Tests (2026-05-01)
- **E2E project:** `tests/E2E/TruckDelivery.E2E.Tests/` — xUnit + Testcontainers (MySQL, MongoDB, Kafka, Redis) + WebApplicationFactory for 5 services (Identity, Order, Driver, Shipment, Payment) + WireMock.Net stubs for Optimizer + Route Service
- **`public partial class Program { }`** added to 5 service Program.cs files (Identity, Order, Driver, Shipment, Payment) to enable WebApplicationFactory<T> referencing
- **`OrderDeliveryFlowTests`:** Full COD flow — create order → Kafka saga assigns driver → pickup → deliver → payment auto-complete; asserts `OrderStatus=AssignedToDriver` and `PaymentStatus=Completed`
- **`BreakdownReassignmentFlowTests`:** Breakdown saga — 2 drivers created; order assigned to driver 1; driver 1 reports breakdown → shipment transitions to `Reassigning`; WireMock reassigns driver 2
- **`WaitForAsync`:** Polling helper with configurable timeout (default 45s) and interval (default 500ms) for async Kafka event propagation
- **`JwtHelper`:** Generates test JWTs directly (bypasses Identity service for test setup) using in-memory `HS256` with test key/issuer/audience
- **`e2e.yml`:** `.github/workflows/e2e.yml` — separate CI workflow, 20 min timeout, runs on push/PR to develop/main/master

### Load Tests — k6 (2026-05-01)
- **`tests/LoadTests/k6/`:** Three k6 scenarios matching project testing rules + golden signals:
  1. **`01-order-creation-load.js`** — 100 VUs ramping over 10 min; creates orders + reads + 20% cancels; thresholds: p95 < 2s, errors < 5%
  2. **`02-tracking-spike.js`** — 500–10k VUs (configurable via `PEAK_VUS` env); GPS updates every 1s; thresholds: p95 < 500ms, ≥ 300k updates in 5 min (= 1k/sec golden signal alert floor)
  3. **`03-kafka-throughput.js`** — 200 order VUs + 1000 tracking VUs simultaneously; estimates Kafka event throughput; queries Prometheus consumer lag via `PROMETHEUS_URL`; lag threshold < 10k messages
- **`lib/auth.js`:** VU-lazy `registerAndLogin()` helper — each VU registers once, reuses token across iterations
- **`lib/data.js`:** `createOrderPayload()` + `locationUpdatePayload()` with randomised coordinates
- **`load-tests.yml`:** `.github/workflows/load-tests.yml` — manual `workflow_dispatch` only (prevents accidental production load); accepts `scenario` choice + `gateway_url` input; installs k6 from official APT repo
- **Results** written to `tests/LoadTests/results/*.json` per run

### Unit Test Expansion (2026-05-02)
- **3 new test projects** added to `tests/Unit/` covering the previously untested services:
  - **`TruckDelivery.Notification.Domain.Tests`** — `NotificationRecordTests` (Create, MarkSent, MarkFailed, all channels/types); `DeviceTokenTests` (Create, platform normalization to lowercase, unique IDs)
  - **`TruckDelivery.Tracking.Domain.Tests`** — `TrackingSessionTests` (Start raises event, UpdateLocation overwrites coords + raises `LocationUpdatedDomainEvent`, Stop sets inactive + preserves last location, full lifecycle)
  - **`TruckDelivery.Analytics.Domain.Tests`** — `BreakdownIncidentTests` (Create, MarkResolved with/without reassignment, recovery time calculation, null vehicleId); `FraudAlertTests` (Create, swapCount threshold > 3, driver symmetry, past detectedAt)
- **Pattern**: xUnit + FluentAssertions, no infrastructure dependencies (pure domain, no mocks needed); same packages as existing 4 unit test projects
- **Note**: project reference paths use 3 `..` (`..\..\..`), not 4 — resolves to repo root then `src/Services/...`

### Grafana Dashboards (2026-05-02)
- **3 dashboard JSON files** provisioned in `docker/grafana/provisioning/dashboards/` — auto-loaded by Grafana on startup via `all.yaml` file provider
  - **`01-fleet-operations.json`** — 8 panels: 4 KPI stats (breakdown count 24h, reassignment success rate gauge, avg recovery time, fraud alerts total) + breakdown by risk level timeseries + reassignment rate timeseries + recovery time timeseries + fraud accumulation timeseries; all panels from Analytics service Prometheus metrics
  - **`02-golden-signals.json`** — 9 panels: 4 stat/KPI (overall error rate, p95 latency, Kafka max lag, tracking events/sec) + HTTP error rate by service + p95 latency by service + Kafka consumer lag by topic + tracking ingestion rate + request throughput by service; refresh 30s
  - **`03-ocr-pipeline.json`** — 8 panels: 4 stats (OCR verified, manual review, rejected counts + auto-verify rate) + donut piechart (verification distribution) + verification outcomes timeseries + extraction p95 duration by document type + extraction rate by document type+status; all from `ocr_extraction_*` and `ocr_verification_*` metrics

### Integration Tests — Driver + Payment (2026-05-02)
- **`tests/Integration/TruckDelivery.Driver.Application.IntegrationTests/`** — Testcontainers MySQL + Redis; 3 test classes (13 tests total):
  - **`DriverRepositoryTests`** — persist + retrieve by Id, find by LicenseNumber, ExistsByIdCardNumber true/false, null for non-existent
  - **`SelfRegisterDriverCommandTests`** — creates Driver + Vehicle in one tx → PendingOcrVerification, outbox entry for `DriverDocumentsSubmitted`, conflict on duplicate IdCard (409), conflict on duplicate UserId, conflict on duplicate LicensePlate
  - **`DriverIdempotencyTests`** — Redis idempotency store: new id returns false, mark+check returns true, prevents duplicate processing, independent ids tracked independently
- **`tests/Integration/TruckDelivery.Payment.Application.IntegrationTests/`** — Testcontainers MySQL only; 3 test classes (13 tests total):
  - **`PaymentRepositoryTests`** — persist Payment + retrieve by Id/OrderId, persist EscrowPayment + retrieve by ShipmentId, null returns for non-existent
  - **`CodPaymentFlowTests`** — COD auto-complete: Payment persisted as Completed, outbox entry for `PaymentCompleted`, conflict on duplicate orderId, correct amount/currency stored
  - **`EscrowPaymentTests`** — create escrow → Locked state, idempotent on duplicate shipmentId (returns same id), Confirm → Released, Dispute → Disputed
- **Both projects** added to `TruckDelivery.slnx` under `/tests/Integration/` folder; `tests/Contract/` folder also added to slnx
- **`integration.yml`** updated: 2 new `dotnet test` steps for Driver + Payment (10 min timeout each)
- **`IUnitOfWork` mismatch fixed**: `Payment.Infrastructure/Persistence/EFCore/UnitOfWork.cs` and `Notification.Infrastructure/Persistence/EFCore/UnitOfWork.cs` corrected from `Task SaveChangesAsync` → `Task<int> SaveChangesAsync` to match `IUnitOfWork` interface

### Contract Tests — Phase 5–7 Events (2026-05-02)
- **4 new event groups** added to `tests/Contract/TruckDelivery.Contracts.Tests/EventSchemaTests.cs`:
  - **`VehicleBreakdownEvent`** (Phase 5) — round-trip with all fields (DriverId, VehicleId?, Lat/Lng, PhotoUrls, TrustScore, FraudRiskLevel), null VehicleId case, forward-compatibility extra-field test
  - **`SuspiciousDriverPairDetectedEvent`** (Phase 6) — round-trip, SwapCount > 3 invariant, OriginalDriverId ≠ ReplacementDriverId
  - **`DriverDocumentsSubmittedEvent`** (Phase 2/OCR) — round-trip with all 7 photo URLs, all-photos-non-empty assertion
  - **`BreakdownReassignmentCompletedEvent`** (Phase 6) — round-trip, different original/replacement drivers assertion
- **`AllIntegrationEvents` MemberData** extended: 6 → 10 events (all 4 new events added to envelope contract theory)
- **`TruckDelivery.Driver.Application`** added as `ProjectReference` to contract test `.csproj`

### Production Deployment Infrastructure (2026-05-02)
- **`docker/docker-compose.yml`** fully completed — all 11 application services now defined:
  - Infrastructure: MySQL, MongoDB, PostGIS, Redis, Kafka (KRaft mode), MinIO, Prometheus, Grafana, Loki, Tempo
  - Init containers: `kafka-init` (creates 19 topics with 3 partitions each via `cp-kafka` entrypoint); `minio-init` (creates `driver-documents` + `breakdown-photos` buckets via `minio/mc`)
  - App services: gateway (:8080), identity (:8081), order (:8082), driver (:8083), route/Rust (:8084), optimizer/Python (:8085), shipment (:8086), tracking (:8087), notification (:8088), payment (:8089), analytics (:8095), ocr (:8090)
  - Networks: `truck-data` (app + DB), `truck-messaging` (app + Kafka), `truck-observability` (app + Grafana stack)
  - All services have healthchecks; gateway `depends_on` all 8 downstream services `condition: service_healthy`
- **Env vars wired in compose:** Notification service: `Firebase__CredentialsJson`, `Twilio__*`, `Smtp__*`, `Notification__AdminEmail`; Payment service: `VnPay__TmnCode/HashSecret/PaymentUrl/ReturnUrl`; Driver service: `MinIO__Endpoint/AccessKey/SecretKey`
- **`docker/.env.example`** updated with all new env vars: Firebase, Twilio, SMTP, VNPay, Notification admin email — all have safe empty/fallback defaults (services fall back to stubs when unset)
- **`.github/workflows/build-test.yml`** updated — added `dotnet test` steps for 3 new unit test projects: Notification, Tracking, Analytics domain tests

### API & Observability Improvements (2026-05-03)
- **Swagger / OpenAPI:** `Swashbuckle.AspNetCore v7.3.1` added to all 8 .NET service `.csproj` files; each service's `Program.cs` registers `AddSwaggerGen` (Bearer JWT security definition) and `app.UseSwagger()` → exposes `/swagger/v1/swagger.json`; **Gateway aggregation:** `UseSwaggerUI` at `/swagger` pulls all 8 specs; YARP routes `swagger-{service}-route` proxy `/swagger/{service}/v1/swagger.json` → each service's `/swagger/v1/swagger.json` with `PathPattern` transform; `Swashbuckle.AspNetCore` added to Gateway `.csproj`
- **Request logging enrichment:** All 8 services' `UseSerilogRequestLogging()` updated to enrich with `CorrelationId` (from `X-Correlation-Id` header), `UserId` (from JWT `NameIdentifier` claim), `UserAgent` — consistent with Gateway enrichment pattern; no request/response body logging (security: passwords, tokens, PII)
- **UnitOfWork decision:** `IUnitOfWork` intentionally keeps only `SaveChangesAsync` — no `BeginTransaction/Commit/Rollback` needed; each command handles one aggregate, entity + OutboxMessage share the same DbContext → single implicit EFCore transaction; explicit transaction methods would be over-engineering given event-driven compensation pattern
- **Data layer ports:** Already exposed in `docker/docker-compose.yml` — MySQL `3306:3306`, MongoDB `27017:27017`, PostGIS `5432:5432`, Redis `6379:6379`; no changes needed

### GPS Batch Endpoint (2026-05-03)
Implemented `POST /api/v1/tracking/batch` to solve offline-cache flush problem (mobile team proposal `docs/mobile-suggestion.md`):
- **Problem:** Driver goes offline → caches up to 100 GPS points → reconnects → flushes 100 requests simultaneously → hits 120 req/min rate limit
- **Solution:** Batch endpoint accepts array of up to 100 points in one HTTP call; rate limit set to 10 req/min (10 batches × 100 points = 1000 points/min capacity)
- **`BatchUpdateLocationCommand`** (`Tracking.Application/Commands/BatchUpdateLocation/`) — handler sorts by `RecordedAt` ASC (client-provided timestamp), calls `ITrackingPointRepository.AddManyAsync()` (MongoDB `InsertManyAsync`), updates session + GPS cache + Kafka + SignalR for **last point only** (most recent; historical catch-up doesn't need N events)
- **`BatchUpdateLocationCommandValidator`** — max 100 points, coordinates range check, `RecordedAt` in past and within 24h
- **`ITrackingPointRepository.AddManyAsync()`** added; implemented via `InsertManyAsync` in `TrackingPointRepository`
- **Gateway:** `tracking-batch-route` YARP entry + `POST:/api/v1/tracking/batch → 10 req/min` rate limit rule in `IpRateLimiting.EndpointRules`
- **API:** `POST /api/v1/tracking/batch` (Driver role) — body: `{ "points": [{ "latitude", "longitude", "recordedAt", "speedKmh?", "headingDeg?" }] }`

### Mobile Integration Documentation
- **Driver App:** `docs/mobile-integration/01-driver-app.md` — Onboarding (3 bước), GPS push, breakdown report, SignalR, FCM; updated 2026-05-01 for Sprint 4 accuracy (all-in-one register, PendingOcrVerification enum, breakdown-photos bucket)
- **Customer App:** `docs/mobile-integration/02-customer-app.md` — Tạo đơn, real-time tracking, thanh toán COD + VNPay, SignalR, FCM; updated 2026-05-01 for Sprint 4 accuracy (firstName/lastName, shipmentId in OrderDto, status/dateFrom/dateTo filters); updated 2026-05-02 — `OrderStatus.Completed=8` added to status table + SignalR `LeaveShipmentGroup` lifecycle corrected (stay connected until Completed not just Delivered)
- **Admin Portal:** `docs/mobile-integration/03-admin-portal.md` — updated 2026-05-02: added Driver list endpoint (`GET /api/v1/drivers?status=&page=`), `assign-vehicle` endpoint, `driverId` filter for vehicles, TrustScore monitoring note; fixed Polling Strategy table (breakdown incidents = poll 1 min, not SignalR); replaced incorrect SignalR `DriverManualReviewRequired` handler with correct FCM/email path + `POST /api/v1/notifications/register-device` for admin device; added §7.2 SignalR tracking hub usage (LocationUpdated, ShipmentStatusUpdated); added §9 System Health (`GET /health/all`)

---

## Shared Projects (Already Exists — Reuse, Don't Recreate)

### `TruckDelivery.Shared.Common`
Path: `src/Shared/TruckDelivery.Shared.Common/`

| Class | Purpose |
|---|---|
| `Domain/AggregateRoot.cs` | Base class for all aggregate roots |
| `Domain/Entity.cs` | Base entity class |
| `Domain/IDomainEvent.cs` | Domain event marker interface |
| `Domain/ValueObject.cs` | Base value object |
| `Exceptions/DomainException.cs` | Domain-level exceptions |
| `Exceptions/NotFoundException.cs` | Not found exception |
| `Persistence/IUnitOfWork.cs` | UoW interface |
| `Primitives/Result.cs` | Result<T> pattern |
| `Primitives/Guard.cs` | Guard clause helpers |
| `Primitives/Error.cs` | Error type |

### `TruckDelivery.Shared.Contracts`
Path: `src/Shared/TruckDelivery.Shared.Contracts/`

| Class | Purpose |
|---|---|
| `Events/IntegrationEvent.cs` | Base Kafka event (has `MessageId`, `OccurredAt`, `SchemaVersion`) |
| `Events/UserRegisteredEvent.cs` | Topic: `userregistered` |

### `TruckDelivery.Shared.Infrastructure`
Path: `src/Shared/TruckDelivery.Shared.Infrastructure/`

| Class | Purpose |
|---|---|
| `Messaging/IEventBus.cs` | Event publishing interface |
| `Messaging/Kafka/KafkaEventBus.cs` | Kafka producer (injects OTel `traceparent`) |
| `Messaging/Kafka/KafkaConsumerBase.cs` | Base `BackgroundService` for consumers |
| `Messaging/Kafka/Idempotency/IIdempotencyStore.cs` | Idempotency check interface |
| `Messaging/Kafka/Idempotency/RedisIdempotencyStore.cs` | Redis-backed, TTL 24h |
| `Caching/ICacheService.cs` | Cache interface |
| `Caching/Redis/RedisCacheService.cs` | Redis implementation |
| `Persistence/IDbConnectionFactory.cs` | Dapper connection factory interface |
| `Persistence/MySql/MySqlConnectionFactory.cs` | MySQL Dapper connection |
| `Telemetry/TelemetryExtensions.cs` | OpenTelemetry DI setup |
| `Extensions/ServiceCollectionExtensions.cs` | Registers Redis, Kafka, MySQL |

---

## Architecture Laws (non-negotiable)

### Microservices + DDD
- Mỗi service là một Bounded Context độc lập
- Services KHÔNG share database với nhau
- Domain model không bị lộ ra ngoài service

### CQRS (strict split)
- **Command (write):** EFCore → MySQL
- **Query (read):** Dapper → MySQL read replica hoặc MongoDB
- **MongoDriver:** chỉ dùng cho MongoDB collections
- Không được dùng EFCore `.ToList()` / `.FirstOrDefault()` trong Query handlers
- Không được dùng Dapper trong Command handlers

### Event-Driven via Kafka
- Cross-service data sync: publish Kafka event TRƯỚC, không gọi HTTP trực tiếp giữa services
- Mỗi Kafka event phải có `MessageId` (UUID) để idempotency check ở consumer
- Mỗi consumer phải có Dead Letter Queue handler
- Kafka header phải carry OpenTelemetry trace context (`traceparent`)

### Saga Pattern (Choreography-based)
- Distributed transaction dùng Choreography Saga qua Kafka events
- Mỗi step phải có compensating transaction event
- Saga state lưu vào MongoDB

### Mediator (MediatR)
- Mọi Command và Query phải đi qua MediatR handler
- Controller chỉ được gọi `_mediator.Send()` — không chứa business logic

### Repository + UnitOfWork
- Repository chỉ expose aggregate root — không expose entity con trực tiếp
- UnitOfWork wrap transaction ở Application layer, không ở Repository hay Domain layer
- Interface định nghĩa ở Domain layer, implementation ở Infrastructure layer

### Spatial & Routing
- PostGIS queries: viết trong Rust service — không viết trong .NET
- OR-Tools (VRP/routing): viết trong Python service — không viết trong .NET
- .NET services giao tiếp với Rust/Python qua HTTP hoặc gRPC

### Non-blocking
- Async/await everywhere trong .NET — không có blocking call (`.Result`, `.Wait()`)
- Rust: dùng `tokio::spawn` — không dùng `std::thread::spawn` cho I/O
- Python: async endpoints với FastAPI + asyncio

---

## Naming Conventions

### .NET
- Commands: `CreateOrderCommand`, `AssignDriverCommand`
- Queries: `GetOrderByIdQuery`, `ListAvailableDriversQuery`
- Command Results: `CreateOrderResult`
- Domain Events: `OrderCreatedDomainEvent`
- Kafka Events: `OrderCreatedEvent`, `DriverAssignedEvent`
- Handlers: `CreateOrderCommandHandler`, `GetOrderByIdQueryHandler`
- Repositories (interface): `IOrderRepository`
- Aggregates: `Order`, `Driver`, `Shipment`

### Kafka Topics
- Format: `{service}.{entity}.{action}` (lowercase, dots)
- Examples: `order.order.created`, `driver.driver.assigned`, `shipment.shipment.status-updated`
- DLQ: `{topic}.dlq`
- Legacy exception: `userregistered` (Identity → Driver, giữ nguyên)
- OCR topics: `driver.documents.submitted` (Driver → OCR), `ocr.driver.verification-completed` (OCR → Driver)

### API Endpoints
- RESTful, versioned: `/api/v1/{resource}`
- Luôn có `X-Correlation-Id` header propagation

---

## Folder Structure Convention (.NET Service)

```
src/Services/{ServiceName}/
  {ServiceName}.Domain/
    Aggregates/           ← Aggregate roots + entities
    ValueObjects/         ← Records (immutable)
    Events/               ← Domain events (internal)
    Repositories/         ← Interfaces only
    Exceptions/           ← Domain-specific exceptions
  {ServiceName}.Application/
    Commands/             ← Command + Handler + Validator per subfolder
    Queries/              ← Query + Handler per subfolder
    Consumers/            ← Kafka BackgroundService consumers
    IntegrationEvents/    ← Kafka event DTOs published by this service
    DTOs/                 ← Read-side data transfer objects
    Interfaces/           ← IUnitOfWork, IEventBus, etc.
    Behaviors/            ← MediatR pipeline behaviors
  {ServiceName}.Infrastructure/
    Persistence/
      EFCore/             ← DbContext, Configurations/, migrations
      Dapper/             ← Read query repos
      Mongo/              ← MongoDB collections, saga state
      Outbox/             ← OutboxMessage, OutboxProcessor
    Messaging/Kafka/
      Producers/          ← Event publishers
      Consumers/          ← DLQ handlers
    Caching/Redis/        ← Idempotency, cache
    Extensions/           ← ServiceCollectionExtensions.cs
  {ServiceName}.Api/
    Controllers/          ← Thin controllers
    Middlewares/          ← GlobalException, CorrelationId
    Program.cs
    appsettings.json
    Dockerfile
```

---

## Code Conventions

### Comments
- Không comment giải thích WHAT — chỉ comment WHY khi thực sự cần thiết
- Không viết multi-line comment blocks
- Không docstring dài

### Error Handling
- Dùng Result pattern hoặc custom Exception cho domain errors
- Không dùng generic `Exception` ở domain layer
- Global exception middleware ở API layer

### Validation
- FluentValidation cho Command validation ở Application layer
- Không validate ở Controller, không validate ở Domain (domain dùng guard clauses)

---

## Every New Service Must Have
- `/health` endpoint (liveness)
- `/ready` endpoint (readiness)
- OpenTelemetry ActivitySource registered
- Serilog structured logging với correlation-id enricher
- Prometheus metrics endpoint (`/metrics`)
- Kafka consumer group với idempotency check (via `RedisIdempotencyStore`)
- Outbox pattern khi publish Kafka events
- Docker-ready `Dockerfile` với multi-stage build

---

## What NOT to Do
- Không dùng EFCore trong Query handlers
- Không gọi HTTP giữa services trong Domain layer
- Không đặt business logic trong Controller
- Không bỏ qua OpenTelemetry tracing khi tạo service mới
- Không viết spatial logic trong .NET — phải là Rust service
- Không viết OR-Tools solver trong .NET — phải là Python service
- Không share domain models giữa các services (dùng DTOs/contracts qua Kafka events)
- Không dùng `.Result` hay `.Wait()` trong async code
- Không publish Kafka event trực tiếp trong Command handler — phải qua Outbox

---

## AI Generation Rules
- Khi generate Entity: phải có private constructor + static factory method
- Khi generate Kafka consumer: kế thừa `KafkaConsumerBase` từ Shared.Infrastructure; luôn có idempotency check bằng `MessageId`
- Khi generate integration event: kế thừa `IntegrationEvent` từ `TruckDelivery.Shared.Contracts`
- Khi generate API endpoint: luôn có correlation-id header propagation
- Khi generate Rust service: dùng `tokio` async runtime
- Khi generate Python service: dùng FastAPI + async/await
- Mọi service mới phải có `/health` và `/ready` endpoint
- Khi generate Saga step: luôn có compensating transaction event
- Khi generate Command handler: phải dùng Outbox pattern (save OutboxMessage trong cùng transaction với entity)
- Không tạo lại các abstractions đã có trong Shared projects — reuse trực tiếp
