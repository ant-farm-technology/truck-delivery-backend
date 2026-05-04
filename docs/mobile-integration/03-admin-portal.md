# Admin Portal Integration Guide

> Audience: Frontend developers building the Admin Dashboard (Next.js / React)
> Base URL: `http://localhost:8080` (API Gateway)
> Cập nhật: 2026-05-02 (Sprint 4 + Phase 5–7)

---

## 1. Authentication

### Login

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "admin@example.com",
  "password": "password"
}
```

Response: `{ "data": { "accessToken": "...", "refreshToken": "...", "expiresAt": "..." } }`

- Store `accessToken` in memory (not localStorage — XSS risk).
- Store `refreshToken` in httpOnly cookie.
- Access token TTL: 1 hour. Refresh token TTL: 30 days.

### Refresh Token

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{ "userId": "<uuid>", "refreshToken": "<token>" }
```

Rotation is enforced: old token is invalidated on each refresh.

### Create Admin Account (Super Admin)

```http
POST /api/v1/admin/accounts
Authorization: Bearer <admin-token>

{
  "email": "newadmin@example.com",
  "password": "securepassword",
  "firstName": "Nguyen",
  "lastName": "Van A"
}
```

---

## 2. Shipment Management

### List Shipments (with filters)

```http
GET /api/v1/shipments?status=DispatcherReviewRequired&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

Useful filter values: `Created`, `RoutePlanning`, `DriverAssigning`, `DispatcherReviewRequired`, `InProgress`, `Completed`, `Failed`, `Reassigning`.

### Confirm Dispatch (after bin-check review)

```http
POST /api/v1/shipments/{id}/confirm-dispatch
Authorization: Bearer <admin-token>
```

Effect: Shipment → `InProgress`. Triggers `ShipmentStartedEvent` → Tracking session created.

### Decline Dispatch

```http
POST /api/v1/shipments/{id}/decline-dispatch
Authorization: Bearer <admin-token>
```

Effect: Shipment → `Failed`. Triggers `ShipmentFailedEvent`.

---

## 3. Driver Management

### List All Drivers (with filters)

```http
GET /api/v1/drivers?status=Available&page=1&pageSize=20
Authorization: Bearer <admin-token>
```

Filter params: `status` (Offline/Available/Busy/Suspended), `page`, `pageSize`.

Response: `PagedResult<DriverDto>` — includes `verificationStatus`, `licenseGrade`, `trustScore` per driver.

### Register Driver (Admin-created)

```http
POST /api/v1/drivers
Authorization: Bearer <admin-token>
Content-Type: application/json

{
  "userId": "<uuid>",
  "fullName": "Trần Văn B",
  ...
}
```

> For driver self-registration flow, see Driver App guide (`01-driver-app.md` §4).

### Assign Vehicle to Driver

```http
POST /api/v1/drivers/{id}/assign-vehicle
Authorization: Bearer <admin-token>

{ "vehicleId": "<uuid>" }
```

### 3.1 Verification Queue

#### List Drivers Pending Verification

```http
GET /api/v1/drivers/pending-verification
Authorization: Bearer <admin-token>
```

Returns drivers with status `PendingOcrVerification` or `ManualReview`. Poll every 30 seconds or check FCM push (see §7).

#### Driver Detail (with verification docs)

```http
GET /api/v1/drivers/{id}
Authorization: Bearer <admin-token>
```

Response includes: `verificationStatus`, `licenseGrade`, `trustScore`, photo URL fields, OCR result fields.

**TrustScore monitoring:** Default 70, range 0–100. Each breakdown report: −3. Collusion detected (swap > 3 times with same driver): −10. Filter drivers with `trustScore < 40` as high-risk.

#### Approve Driver

```http
POST /api/v1/drivers/{id}/verify
Authorization: Bearer <admin-token>
```

#### Reject Driver

```http
POST /api/v1/drivers/{id}/reject-verification
Authorization: Bearer <admin-token>

{ "reason": "ID card does not match selfie" }
```

---

## 4. Vehicle Management

### List Vehicles

```http
GET /api/v1/vehicles?status=Available&type=Truck5T&driverId=<uuid>&page=1
Authorization: Bearer <admin-token>
```

Filter params: `status` (Available/InUse/Maintenance/Breakdown), `type` (Motorbike/Van/Truck3T/Truck5T/Truck10T/Truck15T), `driverId` (vehicles assigned to a specific driver).

### Update Vehicle Status

```http
PUT /api/v1/vehicles/{id}/status
Authorization: Bearer <admin-token>

{ "status": "Maintenance" }
```

Valid values: `Available`, `Maintenance`.

---

## 5. Analytics & KPIs

All analytics endpoints require Admin role. Enforced at both Gateway level (`AdminOnly` policy) and controller level (`[Authorize(Roles = "Admin")]`).

### KPI Dashboard

```http
GET /api/v1/analytics/kpis?days=30
Authorization: Bearer <admin-token>
```

Response:
```json
{
  "breakdownCount": 12,
  "reassignmentSuccessRate": 91.7,
  "avgRecoveryTimeMinutes": 23.4,
  "fraudAlerts": 2
}
```

### Breakdown Incidents

```http
GET /api/v1/analytics/breakdown/incidents?page=1
Authorization: Bearer <admin-token>
```

### Fraud Alerts

```http
GET /api/v1/analytics/fraud/alerts
Authorization: Bearer <admin-token>
```

### Acknowledge Fraud Alert

```http
POST /api/v1/analytics/fraud/alerts/{id}/acknowledge
Authorization: Bearer <admin-token>
```

---

## 6. Payment Management

### List Payments

```http
GET /api/v1/payments?status=Completed&dateFrom=2026-01-01&dateTo=2026-12-31&page=1
Authorization: Bearer <admin-token>
```

### Escrow Operations

```http
# View escrow for a breakdown
GET /api/v1/payments/orders/{orderId}/escrow
Authorization: Bearer <admin-token>

# Confirm escrow release
POST /api/v1/payments/escrow/{id}/confirm
Authorization: Bearer <admin-token>

# Dispute escrow
POST /api/v1/payments/escrow/{id}/dispute
Authorization: Bearer <admin-token>
```

---

## 7. Real-Time Alerts

### 7.1 FCM Push Notifications (Admin Device)

When a driver's OCR result requires manual review, the Notification service sends:
- **FCM push** to the admin's registered device token
- **Email** to `Notification:AdminEmail` (configured via env var)

To receive FCM push on an Admin device, register the FCM token after login:

```http
POST /api/v1/notifications/register-device
Authorization: Bearer <admin-token>

{
  "token": "fcm-device-token-here...",
  "platform": "Android"    // or "Ios"
}
```

FCM payload for manual review alert:
```json
{
  "notification": {
    "title": "Driver Manual Review Required",
    "body": "Driver Trần Văn B needs manual verification"
  },
  "data": {
    "type": "DRIVER_MANUAL_REVIEW_REQUIRED",
    "driverId": "7b2f4c8e-..."
  }
}
```

### 7.2 SignalR — Tracking Hub

The `/hubs/tracking` hub is primarily for real-time GPS tracking. Admin dashboards can connect to monitor active shipments:

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:8080/hubs/tracking", {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build();

await connection.start();

// Join a shipment group to track driver location
await connection.invoke("JoinShipmentGroup", shipmentId);

// Receive real-time GPS updates
connection.on("LocationUpdated", (payload) => {
  // payload: { shipmentId, driverId, latitude, longitude, recordedAt }
  updateMapMarker(payload);
});

// Receive shipment status changes
connection.on("ShipmentStatusUpdated", (payload) => {
  // payload: { shipmentId, status, updatedAt }
  refreshShipmentList();
});
```

> **Note:** For `DriverManualReviewRequired` alerts in a web admin portal (no FCM), rely on email notification or poll `GET /api/v1/drivers/pending-verification` every 30 seconds.

---

## 8. Polling Strategy

| Data | Strategy | Interval |
|---|---|---|
| Pending verification queue | Poll | 30s |
| Active shipments | Poll | 10s |
| KPI dashboard | Poll | 1 min |
| Fraud alerts | Poll | 5 min |
| Breakdown incidents | Poll | 1 min |
| Driver manual review alert | FCM push + email | Real-time |
| Active shipment map | SignalR `LocationUpdated` | Real-time |

---

## 9. System Health

### Aggregate Health Check

```http
GET /health/all
```

No authentication required — endpoint is public (anonymous). Returns health status of all 8 downstream services:

```json
{
  "status": "Healthy",
  "services": {
    "identity": "Healthy",
    "order": "Healthy",
    "driver": "Healthy",
    "shipment": "Healthy",
    "tracking": "Healthy",
    "notification": "Healthy",
    "payment": "Healthy",
    "analytics": "Healthy"
  }
}
```

Use this endpoint in the admin dashboard header to show a system status indicator. Poll every 60 seconds.

---

## 10. Error Handling

All APIs return:
```json
{
  "success": false,
  "error": { "code": "SHIPMENT_NOT_FOUND", "message": "Shipment not found" },
  "meta": { "correlationId": "uuid" }
}
```

| HTTP Code | Meaning |
|---|---|
| 400 | Validation error |
| 401 | Token expired — refresh |
| 403 | Not Admin role |
| 404 | Resource not found |
| 409 | Conflict (duplicate) |
| 422 | Domain rule violation |
| 429 | Rate limit hit |
