# Admin Portal Integration Guide

> Audience: Frontend developers building the Admin Dashboard (Next.js / React)
> Base URL: `http://localhost:8080` (API Gateway)

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

## 3. Driver Verification Queue

### List Drivers Pending Verification

```http
GET /api/v1/drivers/pending-verification
Authorization: Bearer <admin-token>
```

Returns drivers with status `PendingOcrVerification` or `ManualReview`. Poll every 30 seconds or use SignalR alert (see §7).

### Driver Detail (with verification docs)

```http
GET /api/v1/drivers/{id}
Authorization: Bearer <admin-token>
```

Response includes: `verificationStatus`, `licenseGrade`, `trustScore`, photo URL fields, OCR result fields.

### Approve Driver

```http
POST /api/v1/drivers/{id}/verify
Authorization: Bearer <admin-token>
```

### Reject Driver

```http
POST /api/v1/drivers/{id}/reject-verification
Authorization: Bearer <admin-token>

{ "reason": "ID card does not match selfie" }
```

---

## 4. Vehicle Management

### List Vehicles

```http
GET /api/v1/vehicles?status=Available&type=Truck5T&page=1
Authorization: Bearer <admin-token>
```

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

## 7. Real-Time Alerts via SignalR

Connect to `/hubs/tracking` with a valid Admin JWT.

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:8080/hubs/tracking", {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build();

await connection.start();

// Receive admin notifications
connection.on("DriverManualReviewRequired", (payload) => {
  // payload: { driverId, driverName, submittedAt }
  showNotification(`Driver ${payload.driverName} awaits manual review`);
});
```

---

## 8. Polling Strategy

| Data | Strategy | Interval |
|---|---|---|
| Pending verification queue | Poll | 30s |
| Active shipments | Poll | 10s |
| KPI dashboard | Poll | 1 min |
| Fraud alerts | Poll | 5 min |
| Breakdown incidents | SignalR push | Real-time |
| Driver manual review | SignalR push | Real-time |

---

## 9. Error Handling

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
