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

### Remaining Gaps (After Sprint 2)
- **Unit/Integration/Contract tests** — 4 unit test projects created; integration and contract tests not yet written (Sprint 3).
- **GitHub Actions** — `build-test.yml` + `docker-publish.yml` created; `integration.yml` not yet created (Sprint 3).
- **Notification SMS/Email still stubs** — `StubSmsSender`/`StubEmailSender` log only; real impl needs Twilio/SMTP (Sprint 4).
- **Customer PhoneNumber/DateOfBirth missing** — User aggregate in Identity and `RegisterUserCommand` not yet updated.
- **Refresh token rotation** — needs verification (Sprint 4, G-S5).
- **GPS rate limit per-user** — currently per-IP; needs `FixedWindowRateLimiter` keyed by JWT sub (Sprint 3, G-T5).
- **Health check aggregate** — Gateway `/health/all` not yet implemented (Sprint 3, G-T6).
- **OCR Docker model bake** — PaddleOCR downloads ~900MB on first run (Sprint 4, G-T4).
- **[G-B9-VERIFIED] Admin accounts:** `POST /api/v1/admin/accounts` verified existing in `Identity.Api/Controllers/AdminController.cs`.
- **[M3-FIXED] DispatchSagaOrchestrator real Route Service call:** `AddressRequest` now accepts `Latitude?`/`Longitude?`; `Order` aggregate stores 4 coordinate fields; `OrderCreatedEvent` (both publisher + consumer-side DTOs) carries coordinates; `Shipment` aggregate stores pickup/delivery coordinates; `DispatchSagaOrchestrator` calls `routeClient.GetRouteAsync()` when coords present, falls back to Haversine when Route Service is unreachable, and falls back to static placeholder when coords are absent; migrations `20260430000001_AddCoordinatesToOrder` + `20260430000001_AddCoordinatesToShipment` created.

### Mobile Integration Documentation
- **Driver App:** `docs/mobile-integration/01-driver-app.md` — Onboarding (3 bước), GPS push, breakdown report, SignalR, FCM
- **Customer App:** `docs/mobile-integration/02-customer-app.md` — Tạo đơn, real-time tracking, thanh toán, SignalR, FCM

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
