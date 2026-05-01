# Database Schema Reference

> Generated: 2026-05-01 | Reflects Phase 1–7 + Sprint 1–4 state

---

## MySQL Databases

### `truck_identity`

| Table | Columns | Notes |
|---|---|---|
| `users` | `Id` (PK), `Email` (unique), `PasswordHash`, `FirstName`, `LastName`, `Role` (int), `IsActive`, `CreatedAt`, `LastLoginAt`, `RefreshToken`, `RefreshTokenExpiresAt`, `PhoneNumber`, `DateOfBirth` | `Role`: Customer=1, Driver=2, Admin=3 |

---

### `truck_order`

| Table | Columns | Notes |
|---|---|---|
| `orders` | `Id` (PK), `CustomerId`, `Status` (int), `TotalWeightKg`, `TotalVolumeCbm`, `ShipmentId` (nullable FK→Shipment), `PickupStreet`, `PickupCity`, `PickupProvince`, `PickupLatitude?`, `PickupLongitude?`, `DeliveryStreet`, `DeliveryCity`, `DeliveryProvince`, `DeliveryLatitude?`, `DeliveryLongitude?`, `CreatedAt`, `UpdatedAt` | Status: Pending=1…Completed=8 |
| `order_items` | `Id` (PK), `OrderId` (FK), `ProductName`, `Quantity`, `WeightKg`, `VolumeCbm`, `LengthM`, `WidthM`, `HeightM`, `CanTilt` (nullable bool) | Dimensions for 3D bin-packing |
| `outbox_messages` | `Id` (PK), `EventType`, `Payload` (JSON), `OccurredAt`, `ProcessedAt?` | Transactional outbox for Kafka publish |

**OrderStatus enum:** Pending=1, Confirmed=2, AssignedToDriver=3, PickedUp=4, InTransit=5, Delivered=6, Cancelled=7, Completed=8

---

### `truck_driver`

| Table | Columns | Notes |
|---|---|---|
| `drivers` | `Id` (PK), `UserId`, `FullName`, `Email`, `Status` (int), `VerificationStatus` (int), `LicenseGrade` (int), `LicenseExpiryDate`, `TrustScore` (int, default 70), `DateOfBirth?`, `Address?`, `IdCardNumber?` (unique), `FrontIdCardUrl`, `BackIdCardUrl`, `SelfieUrl`, `FrontLicenseUrl`, `BackLicenseUrl`, `VehicleRegUrl`, `VehicleFrontUrl`, `OcrRawResult?` (JSON), `OcrVerifiedAt?`, `AdminVerifiedAt?`, `AdminVerifiedBy?`, `RejectionReason?`, `CurrentVehicleId?` (FK), `CreatedAt`, `UpdatedAt` | |
| `vehicles` | `Id` (PK), `LicensePlate`, `Type` (int), `MaxWeightKg`, `VolumeCbm`, `LengthM?`, `WidthM?`, `HeightM?`, `RegistrationNumber?`, `RegistrationExpiryDate?`, `Status` (int), `AssignedDriverId?` (FK), `CreatedAt` | VehicleStatus: Available=1, InUse=2, Maintenance=3, Breakdown=4 |
| `breakdown_reports` | `Id` (PK), `DriverId` (FK), `VehicleId` (FK), `Latitude`, `Longitude`, `RiskLevel` (int), `PhotoUrls` (JSON), `ReportedAt` | FraudRiskLevel: Unknown/Low/Medium/High/Confirmed |
| `driver_swap_records` | `Id` (PK), `OriginalDriverId`, `ReplacementDriverId`, `ShipmentId`, `CreatedAt` | Collusion detection input |
| `outbox_messages` | same pattern | |

**DriverStatus:** Offline=1, Available=2, Busy=3, Suspended=4
**VerificationStatus:** Draft=1, PendingOcrVerification=2, OcrVerified=3, ManualReview=4, AdminVerified=5, Rejected=6
**LicenseGrade:** B1=1, B2=2, C=3, D=4, E=5, FC=6, FD=7

---

### `truck_shipment`

| Table | Columns | Notes |
|---|---|---|
| `shipments` | `Id` (PK), `OrderId`, `CustomerId`, `Status` (int), `DriverId?`, `VehicleId?`, `OriginalBreakdownDriverId?`, `IsBreakdownReassignment` (bool), `Packages` (JSON), `PickupLatitude?`, `PickupLongitude?`, `DeliveryLatitude?`, `DeliveryLongitude?`, `DistanceMeters?`, `FailureReason?`, `CreatedAt`, `UpdatedAt` | |
| `outbox_messages` | same pattern | |

**ShipmentStatus:** Created=1, RoutePlanning=2, DriverAssigning=3, DriverConfirmed=4, InProgress=5, Completed=6, Failed=7, DispatcherReviewRequired=8, Reassigning=9

---

### `truck_notification`

| Table | Columns | Notes |
|---|---|---|
| `notification_records` | `Id` (PK), `UserId`, `Type` (int), `Title`, `Body`, `Channel` (int), `SentAt?`, `FailedAt?`, `RetryCount`, `CreatedAt` | |
| `device_tokens` | `Id` (PK), `UserId`, `Platform` (int), `Token`, `UpdatedAt` | Unique on (UserId, Platform) |
| `outbox_messages` | same pattern | |

---

### `truck_payment`

| Table | Columns | Notes |
|---|---|---|
| `payments` | `Id` (PK), `OrderId`, `CustomerId`, `Amount`, `Currency` (default VND), `Method` (int), `Status` (int), `TransactionId?`, `CreatedAt`, `UpdatedAt` | Method: Cod=1, VnPay=2 |
| `escrow_payments` | `Id` (PK), `ShipmentId`, `OriginalDriverId`, `ReplacementDriverId`, `Amount` (default 50000), `Status` (int), `CreatedAt`, `ResolvedAt?` | EscrowStatus: Locked/Released/Disputed/Refunded |
| `outbox_messages` | same pattern | |

**PaymentStatus:** Created=1, Pending=2, Authorized=3, Captured=4, Completed=5, Failed=6, Refunded=7

---

## MongoDB Collections

### `truck_tracking` database

| Collection | Document shape | Notes |
|---|---|---|
| `tracking_sessions` | `{ _id, shipmentId, driverId, customerId, status, startedAt, endedAt? }` | Active/Completed |
| `tracking_points` | `{ _id, sessionId, driverId, latitude, longitude, accuracy?, recordedAt }` | GPS trail |

### `truck_analytics` database

| Collection | Document shape | Notes |
|---|---|---|
| `breakdown_incidents` | `{ _id, driverId, vehicleId, shipmentId?, riskLevel, reportedAt, resolvedAt?, isSuccessfullyReassigned, recoveryTimeMinutes? }` | |
| `fraud_alerts` | `{ _id, originalDriverId, replacementDriverId, swapCount, detectedAt, isAcknowledged, acknowledgedAt? }` | |

### Saga state collections (in `truck_shipment` MongoDB)

| Collection | Document shape |
|---|---|
| `shipment_saga_states` | `{ _id (sagaId), orderId, shipmentId, status, completedSteps[], retryCount, startedAt, ... }` |
| `breakdown_saga_states` | `{ _id, shipmentId, originalDriverId, status, retryCount, startedAt, ... }` |

---

## PostGIS Database (`truck_routing`)

| Table | Columns | Notes |
|---|---|---|
| `driver_locations` | `driver_id` (PK), `location` (GEOMETRY Point), `updated_at` | Current position per driver |
| `road_network` | `id` (PK), `source`, `target`, `cost`, `reverse_cost`, `geom` (GEOMETRY LineString) | OSM road graph |

---

## Key Relationships

```
users (Identity) ──userId──▶ drivers (Driver) — via Kafka UserRegisteredEvent
orders (Order) ──orderId──▶ shipments (Shipment) — via Kafka OrderCreatedEvent
shipments ──orderId──▶ payments (Payment) — via Kafka ShipmentCompletedEvent
shipments ──shipmentId──▶ tracking_sessions (Tracking) — via Kafka ShipmentStartedEvent
drivers ──driverId──▶ tracking_sessions — active session
```

Services **do not share databases** — all cross-service references are IDs only, synchronized via Kafka events.
