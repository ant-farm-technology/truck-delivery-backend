# Truck Delivery — API Reference

> Version: 1.0 | Base URL: `http://localhost:8080` (API Gateway)
> All requests go through the API Gateway (YARP, port 8080).
> Internal services (Route :8084, Optimizer :8085) are not exposed externally.

---

## Table of Contents

1. [Authentication & Headers](#1-authentication--headers)
2. [Identity Service — `/api/v1/auth`](#2-identity-service)
3. [Order Service — `/api/v1/orders`](#3-order-service)
4. [Driver Service — `/api/v1/drivers`](#4-driver-service)
5. [Vehicle Service — `/api/v1/vehicles`](#5-vehicle-service)
6. [Shipment Service — `/api/v1/shipments`](#6-shipment-service)
7. [Tracking Service — `/api/v1/tracking`](#7-tracking-service)
8. [Payment Service — `/api/v1/payments`](#8-payment-service)
9. [Analytics Service — `/api/v1/analytics`](#9-analytics-service)
10. [Real-time — SignalR Hub `/hubs/tracking`](#10-real-time--signalr-hub)
11. [Enums Reference](#11-enums-reference)
12. [Error Responses](#12-error-responses)

---

## 1. Authentication & Headers

### JWT Authentication

All endpoints (except Auth) require a Bearer token:

```http
Authorization: Bearer <access_token>
Content-Type: application/json
X-Correlation-Id: <uuid>
```

`X-Correlation-Id` is optional but strongly recommended for tracing. The Gateway injects one automatically if omitted.

### Roles

| Role | Applies to |
|---|---|
| `Customer` | End users placing delivery orders |
| `Driver` | Truck drivers |
| `Admin` | Operations staff with full system access |

---

## 2. Identity Service

**Gateway route:** `/api/v1/auth/*` → Identity Service `:8081`

### POST /api/v1/auth/register

Register a new user account.

**Auth:** Anonymous

**Request body:**

```json
{
  "email": "user@example.com",
  "password": "P@ssw0rd123",
  "firstName": "Nguyen",
  "lastName": "Van A"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `email` | string | Yes | Must be unique |
| `password` | string | Yes | Min 8 chars |
| `firstName` | string | Yes | |
| `lastName` | string | Yes | |

**Response `201 Created`:**

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com"
}
```

**Error responses:**
- `400 Bad Request` — validation failure or email already registered

---

### POST /api/v1/auth/login

Authenticate and receive tokens.

**Auth:** Anonymous

**Request body:**

```json
{
  "email": "user@example.com",
  "password": "P@ssw0rd123"
}
```

**Response `200 OK`:**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "expiresAt": "2026-04-30T11:00:00Z"
}
```

| Field | Type | Notes |
|---|---|---|
| `accessToken` | string | JWT, use in `Authorization: Bearer` header |
| `refreshToken` | string | UUID, use to obtain new access token |
| `expiresAt` | datetime (ISO 8601) | UTC expiry time of the access token |

**Error responses:**
- `401 Unauthorized` — invalid email or password

---

### POST /api/v1/auth/refresh

Refresh an expired access token.

**Auth:** Anonymous

**Request body:**

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "refreshToken": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

**Response `200 OK`:** Same shape as `/login` response.

**Error responses:**
- `401 Unauthorized` — refresh token invalid or expired

---

## 3. Order Service

**Gateway route:** `/api/v1/orders/*` → Order Service `:8082`

### POST /api/v1/orders

Create a new delivery order.

**Auth:** Bearer (any role)

**Request body:**

```json
{
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "pickupAddress": {
    "street": "123 Nguyen Hue",
    "city": "Ho Chi Minh City",
    "province": "Ho Chi Minh",
    "postalCode": "700000",
    "country": "VN"
  },
  "deliveryAddress": {
    "street": "456 Le Loi",
    "city": "Hanoi",
    "province": "Hanoi",
    "postalCode": "100000",
    "country": "VN"
  },
  "items": [
    {
      "productName": "Samsung Refrigerator",
      "quantity": 1,
      "weightKg": 45.0,
      "volumeCbm": 0.35,
      "lengthM": 0.6,
      "widthM": 0.7,
      "heightM": 1.8,
      "canTilt": false,
      "notes": "Handle with care"
    }
  ],
  "notes": "Call before delivery"
}
```

**AddressRequest fields:**

| Field | Type | Required | Notes |
|---|---|---|---|
| `street` | string | Yes | |
| `city` | string | Yes | |
| `province` | string | Yes | |
| `postalCode` | string | No | |
| `country` | string | No | Default: `"VN"` |

**OrderItemRequest fields:**

| Field | Type | Required | Notes |
|---|---|---|---|
| `productName` | string | Yes | |
| `quantity` | int | Yes | Min: 1 |
| `weightKg` | decimal | Yes | Per unit, must be > 0 |
| `volumeCbm` | decimal | Yes | Per unit, must be > 0 |
| `lengthM` | decimal | No | Used for 3D bin-check |
| `widthM` | decimal | No | Used for 3D bin-check |
| `heightM` | decimal | No | Used for 3D bin-check |
| `canTilt` | bool | No | Default: false. If false, item cannot be laid on its side |
| `notes` | string | No | |

> **Note:** `lengthM`, `widthM`, `heightM` enable automatic bin-check. Without them, dispatch requires manual Admin confirmation.

**Response `201 Created`:**

```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2026-04-30T09:00:00Z"
}
```

**Error responses:**
- `400 Bad Request` — validation failure

---

### GET /api/v1/orders/{id}

Get a single order by ID.

**Auth:** Bearer (any role)

**Path parameters:**

| Param | Type | Notes |
|---|---|---|
| `id` | GUID | Order ID |

**Response `200 OK`:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "...",
  "status": "Pending",
  "pickupStreet": "123 Nguyen Hue",
  "pickupCity": "Ho Chi Minh City",
  "pickupProvince": "Ho Chi Minh",
  "deliveryStreet": "456 Le Loi",
  "deliveryCity": "Hanoi",
  "deliveryProvince": "Hanoi",
  "totalWeightKg": 45.0,
  "totalVolumeCbm": 0.35,
  "notes": "Call before delivery",
  "cancellationReason": null,
  "createdAt": "2026-04-30T09:00:00Z",
  "updatedAt": "2026-04-30T09:00:00Z",
  "items": [
    {
      "id": "...",
      "productName": "Samsung Refrigerator",
      "quantity": 1,
      "weightKg": 45.0,
      "volumeCbm": 0.35,
      "notes": "Handle with care"
    }
  ]
}
```

**Error responses:**
- `404 Not Found` — order does not exist

---

### GET /api/v1/orders

List orders for a customer (paginated).

**Auth:** Bearer (any role)

**Query parameters:**

| Param | Type | Required | Default | Notes |
|---|---|---|---|---|
| `customerId` | GUID | Yes | — | Filter by customer |
| `page` | int | No | 1 | 1-based |
| `pageSize` | int | No | 20 | Max 100 |

**Response `200 OK`:** Array of `OrderSummaryDto`:

```json
[
  {
    "id": "...",
    "customerId": "...",
    "status": "InProgress",
    "pickupCity": "Ho Chi Minh City",
    "deliveryCity": "Hanoi",
    "totalWeightKg": 45.0,
    "createdAt": "2026-04-30T09:00:00Z"
  }
]
```

---

### DELETE /api/v1/orders/{id}

Cancel an order. Only allowed when status is `Pending` or `Confirmed`.

**Auth:** Bearer (any role — requester ID taken from JWT `sub` claim)

**Path parameters:**

| Param | Type | Notes |
|---|---|---|
| `id` | GUID | Order ID |

**Request body:**

```json
{
  "reason": "Customer changed plans"
}
```

**Response `204 No Content`**

**Error responses:**
- `400 Bad Request` — invalid transition (e.g., order already shipped)
- `401 Unauthorized` — missing or invalid token

---

## 4. Driver Service

**Gateway route:** `/api/v1/drivers/*` → Driver Service `:8083`

### POST /api/v1/drivers

Register a new driver profile. Requires the user to have been registered first via `/api/v1/auth/register`.

**Auth:** Bearer — Role: `Admin`

**Request body:**

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "driver@example.com",
  "firstName": "Tran",
  "lastName": "Van B",
  "phoneNumber": "0901234567",
  "licenseNumber": "B2-123456"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `userId` | GUID | Yes | Must match an existing Identity user |
| `email` | string | Yes | |
| `firstName` | string | Yes | |
| `lastName` | string | Yes | |
| `phoneNumber` | string | Yes | |
| `licenseNumber` | string | Yes | Driver's license number |

**Response `201 Created`:**

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000"
}
```

---

### GET /api/v1/drivers/{id}

Get driver details by ID.

**Auth:** Bearer (any role)

**Response `200 OK`:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "driver@example.com",
  "firstName": "Tran",
  "lastName": "Van B",
  "phoneNumber": "0901234567",
  "licenseNumber": "B2-123456",
  "status": "Available",
  "currentVehicleId": "...",
  "isActive": true,
  "createdAt": "2026-04-30T08:00:00Z"
}
```

**Error responses:**
- `404 Not Found`

---

### GET /api/v1/drivers/available

List all drivers with status `Available`.

**Auth:** Bearer (any role)

**Response `200 OK`:** Array of `DriverDto` (same shape as GET by ID).

---

### PUT /api/v1/drivers/{id}/status

Update driver availability status.

**Auth:** Bearer (any role)

**Request body:**

```json
{
  "status": "Available"
}
```

`status` values: `Offline` | `Available` | `Busy` | `Suspended`

**Response `204 No Content`**

**Error responses:**
- `400 Bad Request` — invalid status value

---

### POST /api/v1/drivers/{id}/assign-vehicle

Assign a vehicle to a driver.

**Auth:** Bearer — Role: `Admin`

**Request body:**

```json
{
  "vehicleId": "550e8400-e29b-41d4-a716-446655440001"
}
```

**Response `204 No Content`**

**Error responses:**
- `400 Bad Request` — vehicle not found, already assigned, or driver not found

---

### POST /api/v1/drivers/{id}/report-breakdown

Driver reports that their vehicle has broken down. Request passes through the anti-fraud gate before being accepted.

**Auth:** Bearer — Role: `Driver`

**Request body:**

```json
{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "photoUrls": [
    "https://storage.example.com/breakdowns/photo1.jpg",
    "https://storage.example.com/breakdowns/photo2.jpg"
  ]
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `latitude` | double | Yes | Current GPS latitude |
| `longitude` | double | Yes | Current GPS longitude |
| `photoUrls` | string[] | Yes | At least 1 photo required |

**Anti-fraud gate checks:**
1. Driver trust score must be ≥ 30
2. At least 1 photo URL required
3. GPS position must be within 2 km of last cached position (Low risk) or further (Medium risk)

**Response `200 OK`:**

```json
{
  "reportId": "550e8400-e29b-41d4-a716-446655440002",
  "fraudRiskLevel": "Low",
  "accepted": true
}
```

| Field | Type | Notes |
|---|---|---|
| `reportId` | GUID | Breakdown report ID |
| `fraudRiskLevel` | string | `Unknown` \| `Low` \| `Medium` \| `High` \| `Confirmed` |
| `accepted` | bool | Whether the breakdown was accepted |

**Response `422 Unprocessable Entity`** — fraud gate rejected (trust score too low, no photo, or GPS anomaly):

```json
{
  "code": "FRAUD_GATE_REJECTED",
  "description": "Trust score below minimum threshold"
}
```

---

## 5. Vehicle Service

**Gateway route:** `/api/v1/vehicles/*` → Driver Service `:8083`

### POST /api/v1/vehicles

Register a new vehicle.

**Auth:** Bearer — Role: `Admin`

**Request body:**

```json
{
  "licensePlate": "51A-12345",
  "brand": "Hyundai",
  "model": "HD120",
  "type": "Truck5T",
  "maxWeightKg": 5000,
  "maxVolumeCbm": 20.0,
  "yearOfManufacture": 2022
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `licensePlate` | string | Yes | Must be unique |
| `brand` | string | Yes | |
| `model` | string | Yes | |
| `type` | string | Yes | See [VehicleType enum](#11-enums-reference) |
| `maxWeightKg` | decimal | Yes | Maximum payload in kg |
| `maxVolumeCbm` | decimal | Yes | Maximum cargo volume in m³ |
| `yearOfManufacture` | int | Yes | |

**Response `201 Created`:**

```json
{
  "vehicleId": "550e8400-e29b-41d4-a716-446655440003"
}
```

---

### GET /api/v1/vehicles/{id}

Get vehicle details.

**Auth:** Bearer (any role)

**Response `200 OK`:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440003",
  "licensePlate": "51A-12345",
  "brand": "Hyundai",
  "model": "HD120",
  "type": "Truck5T",
  "maxWeightKg": 5000,
  "maxVolumeCbm": 20.0,
  "yearOfManufacture": 2022,
  "status": "Available",
  "assignedDriverId": null,
  "createdAt": "2026-04-30T08:00:00Z"
}
```

**Error responses:**
- `404 Not Found`

---

## 6. Shipment Service

**Gateway route:** `/api/v1/shipments/*` → Shipment Service `:8086`

> Shipments are created automatically when an Order is created (via Kafka event, not a direct API call). The Saga orchestrator handles driver assignment.

### GET /api/v1/shipments/{id}

Get shipment details.

**Auth:** Bearer (any role)

**Response `200 OK`:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440004",
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "...",
  "status": "InProgress",
  "pickupCity": "Ho Chi Minh City",
  "pickupProvince": "Ho Chi Minh",
  "deliveryCity": "Hanoi",
  "deliveryProvince": "Hanoi",
  "totalWeightKg": 45.0,
  "totalVolumeCbm": 0.35,
  "assignedDriverId": "...",
  "assignedVehicleId": "...",
  "distanceMeters": 1720000.0,
  "failureReason": null,
  "createdAt": "2026-04-30T09:00:00Z",
  "updatedAt": "2026-04-30T09:30:00Z"
}
```

**Shipment status values:**

| Status | Description |
|---|---|
| `Created` | Shipment created, awaiting route planning |
| `RoutePlanning` | Calling Route Service to calculate distance |
| `DriverAssigning` | Saga is selecting and assigning a driver |
| `DriverConfirmed` | Driver assigned and confirmed |
| `DispatcherReviewRequired` | Bin-check flagged manual review needed |
| `InProgress` | Driver picked up and en route |
| `Reassigning` | Original driver broke down, re-assigning |
| `Completed` | Delivered successfully |
| `Failed` | All retry attempts exhausted |

**Error responses:**
- `404 Not Found`

---

### POST /api/v1/shipments/{id}/confirm-dispatch

Manually approve a shipment that is pending dispatcher review (`DispatcherReviewRequired` status). Transitions the shipment to `InProgress`.

**Auth:** Bearer — Role: `Admin`

**Response `204 No Content`**

**Error responses:**
- `400 Bad Request` — shipment not in `DispatcherReviewRequired` status
- `404 Not Found`

---

### PUT /api/v1/shipments/{id}/status

Update shipment status. Used by Driver to mark pickup/delivery milestones.

**Auth:** Bearer — Role: `Admin` or `Driver`

**Request body:**

```json
{
  "status": "PickedUp"
}
```

Valid driver-settable status values: `PickedUp` | `InTransit` | `Delivered`

**Response `204 No Content`**

**Error responses:**
- `400 Bad Request` — invalid status string or invalid transition

---

## 7. Tracking Service

**Gateway route:** `/api/v1/tracking/*` → Tracking Service `:8087`

### POST /api/v1/tracking/location

Push a GPS location update. Called by the Driver app on a 1–5 second interval while shipment is active.

**Auth:** Bearer — Role: `Driver`

Driver ID is extracted from the JWT `sub` claim automatically — no need to include it in the body.

**Request body:**

```json
{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "speedKmh": 45.5,
  "headingDeg": 270.0
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `latitude` | double | Yes | WGS-84 latitude (-90 to 90) |
| `longitude` | double | Yes | WGS-84 longitude (-180 to 180) |
| `speedKmh` | double | No | Current speed in km/h |
| `headingDeg` | double | No | Bearing in degrees (0 = North) |

**Response `204 No Content`**

**Error responses:**
- `400 Bad Request` — invalid coordinates or no active shipment for driver

> **Performance note:** Update every 1s when moving, every 5s when stationary. Batch and replay cached points if network was lost.

---

### GET /api/v1/tracking/shipments/{shipmentId}/points

Get recent GPS trail for a shipment.

**Auth:** Bearer (any role)

**Path parameters:**

| Param | Type | Notes |
|---|---|---|
| `shipmentId` | GUID | Shipment ID |

**Query parameters:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `limit` | int | 100 | Max number of points to return (newest first) |

**Response `200 OK`:**

```json
[
  {
    "driverId": "550e8400-e29b-41d4-a716-446655440005",
    "latitude": 10.7812,
    "longitude": 106.6987,
    "speedKmh": 52.0,
    "recordedAt": "2026-04-30T10:06:00Z"
  },
  {
    "driverId": "550e8400-e29b-41d4-a716-446655440005",
    "latitude": 10.7769,
    "longitude": 106.7009,
    "speedKmh": 45.5,
    "recordedAt": "2026-04-30T10:05:00Z"
  }
]
```

Points are returned in descending time order (newest first).

---

## 8. Payment Service

**Gateway route:** `/api/v1/payments/*` → Payment Service `:8089`

> COD payments are created automatically when an order is delivered (Kafka event). For VNPay, use `POST /orders/{orderId}/initiate`.

### POST /api/v1/payments/orders/{orderId}/initiate

Initiate a payment for an order (Customer use). For COD, returns `paymentUrl: null`. For VNPay, returns a redirect URL.

**Auth:** Bearer — Role: `Customer`

**Request body:**

```json
{
  "customerId": "550e8400-e29b-41d4-a716-446655440006",
  "amount": 250000,
  "method": "VnPay",
  "currency": "VND"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `customerId` | GUID | Yes | Must match authenticated user |
| `amount` | decimal | Yes | In VND |
| `method` | string | No | `"Cod"` (default) or `"VnPay"` |
| `currency` | string | No | Default: `"VND"` |

**Response `200 OK`:**

```json
{
  "paymentId": "550e8400-e29b-41d4-a716-446655440007",
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_Amount=..."
}
```

`paymentUrl` is `null` for COD. For VNPay, redirect the client to this URL.

**Error responses:**
- `409 Conflict` — payment already exists for this order
- `400 Bad Request` — validation failure

---

### GET /api/v1/payments/webhook/vnpay  
### POST /api/v1/payments/webhook/vnpay

VNPay IPN / return URL handler. Called by VNPay after payment completion.

**Auth:** Anonymous (VNPay callback)

**Query parameters:** VNPay standard params (`vnp_TxnRef`, `vnp_ResponseCode`, `vnp_SecureHash`, etc.)

**Response `200 OK`:**
```json
{ "RspCode": "00", "Message": "Confirmed" }
```

---

### POST /api/v1/payments

Manually create a payment record (Admin use).

**Auth:** Bearer — Role: `Admin`

**Request body:**

```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "550e8400-e29b-41d4-a716-446655440006",
  "amount": 250000,
  "currency": "VND"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `orderId` | GUID | Yes | |
| `customerId` | GUID | Yes | |
| `amount` | decimal | Yes | |
| `currency` | string | No | Default: `"VND"` |

**Response `201 Created`:**

```json
{
  "paymentId": "550e8400-e29b-41d4-a716-446655440007"
}
```

**Error responses:**
- `409 Conflict` — payment already exists for this order

---

### GET /api/v1/payments/orders/{orderId}

Get payment info for an order.

**Auth:** Bearer (any role)

**Response `200 OK`:**

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440007",
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "customerId": "...",
  "amount": 250000,
  "currency": "VND",
  "status": "Completed",
  "failureReason": null,
  "createdAt": "2026-04-30T12:00:00Z"
}
```

**Payment status values:** `Created` | `Pending` | `Authorized` | `Captured` | `Completed` | `Failed` | `Refunded`

**Error responses:**
- `404 Not Found`

---

### POST /api/v1/payments/escrow/{id}/confirm

Confirm an escrow payment (surcharge for breakdown reassignment). Releases funds.

**Auth:** Bearer — Role: `Customer` or `Admin`

**Path parameters:**

| Param | Type | Notes |
|---|---|---|
| `id` | GUID | Escrow payment ID |

**Request body:**

```json
{
  "note": "Accepted the breakdown surcharge"
}
```

`note` is optional.

**Response `204 No Content`**

**Error responses:**
- `404 Not Found` — escrow not found
- `409 Conflict` — escrow already resolved

---

### POST /api/v1/payments/escrow/{id}/dispute

Dispute an escrow payment (customer disputes the surcharge).

**Auth:** Bearer — Role: `Customer` or `Admin`

**Request body:**

```json
{
  "note": "The breakdown was suspicious"
}
```

**Response `204 No Content`**

**Error responses:**
- `404 Not Found`
- `409 Conflict` — already resolved

> **Escrow context:** When a driver reports a breakdown and the shipment is reassigned, the system automatically locks a 50,000 VND surcharge fee as escrow. The customer can confirm (accept charge) or dispute it. Disputed escrows are reviewed by Admin.

---

## 8b. Notification Service

**Gateway route:** `/api/v1/notifications/*` → Notification Service `:8088`

### POST /api/v1/notifications/register-device

Register or update a device token for push notifications (FCM).

**Auth:** Bearer (any role)

**Request body:**

```json
{
  "deviceToken": "fcm-device-token-string",
  "platform": "Android"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `deviceToken` | string | Yes | FCM registration token from device |
| `platform` | string | Yes | `"Android"` or `"Ios"` |

**Response `200 OK`:** Empty (upsert — one token per userId+platform)

---

## 9. Analytics Service

**Gateway route:** `/api/v1/analytics/*` → Analytics Service `:8095`

All analytics endpoints require **Admin** role.

### GET /api/v1/analytics/kpis

Get aggregated KPI snapshot for the specified period.

**Auth:** Bearer — Role: `Admin`

**Query parameters:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `days` | int | 30 | Lookback period in days |

**Response `200 OK`:**

```json
{
  "periodDays": 30,
  "breakdownCount": 42,
  "successfulReassignmentCount": 38,
  "reassignmentSuccessRatePct": 90.5,
  "avgRecoveryTimeMinutes": 23.7,
  "fraudAlertCount": 3,
  "breakdownsByRiskLevel": {
    "Low": 35,
    "Medium": 6,
    "High": 1
  }
}
```

| Field | Type | Notes |
|---|---|---|
| `periodDays` | int | Echoed back from query param |
| `breakdownCount` | long | Total breakdown incidents reported |
| `successfulReassignmentCount` | long | Incidents where driver was successfully reassigned |
| `reassignmentSuccessRatePct` | double | `successfulReassignmentCount / breakdownCount * 100` |
| `avgRecoveryTimeMinutes` | double? | Average minutes from breakdown report to successful reassignment. `null` if no data. |
| `fraudAlertCount` | long | Total suspicious driver-pair detections |
| `breakdownsByRiskLevel` | dict | Count per fraud risk level (`Low`, `Medium`, `High`, `Confirmed`) |

---

### GET /api/v1/analytics/breakdown/incidents

List breakdown incidents.

**Auth:** Bearer — Role: `Admin`

**Query parameters:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `days` | int | 30 | Lookback period |
| `limit` | int | 50 | Max records to return |

**Response `200 OK`:**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440008",
    "driverId": "550e8400-e29b-41d4-a716-446655440005",
    "vehicleId": "550e8400-e29b-41d4-a716-446655440003",
    "shipmentId": "550e8400-e29b-41d4-a716-446655440004",
    "fraudRiskLevel": "Low",
    "latitude": 10.7769,
    "longitude": 106.7009,
    "reportedAt": "2026-04-30T09:00:00Z",
    "resolvedAt": "2026-04-30T09:23:00Z",
    "isResolved": true,
    "isSuccessfullyReassigned": true,
    "recoveryTimeMinutes": 23
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `id` | GUID | Incident ID |
| `driverId` | GUID | Driver who reported breakdown |
| `vehicleId` | GUID? | Vehicle that broke down |
| `shipmentId` | GUID? | Associated shipment (set when resolved) |
| `fraudRiskLevel` | string | `Unknown` \| `Low` \| `Medium` \| `High` \| `Confirmed` |
| `latitude` | double | Reported breakdown location |
| `longitude` | double | |
| `reportedAt` | datetime | UTC |
| `resolvedAt` | datetime? | UTC, null if still open |
| `isResolved` | bool | |
| `isSuccessfullyReassigned` | bool | True if a replacement driver was found |
| `recoveryTimeMinutes` | int? | Minutes from report to reassignment |

---

### GET /api/v1/analytics/fraud/alerts

List fraud (collusion) alerts detected by the system.

**Auth:** Bearer — Role: `Admin`

**Query parameters:**

| Param | Type | Default | Notes |
|---|---|---|---|
| `days` | int | 30 | Lookback period |
| `limit` | int | 50 | Max records |

**Response `200 OK`:**

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440009",
    "originalDriverId": "550e8400-e29b-41d4-a716-446655440005",
    "replacementDriverId": "550e8400-e29b-41d4-a716-44665544000a",
    "swapCount": 4,
    "detectedAt": "2026-04-30T02:00:00Z",
    "isAcknowledged": false
  }
]
```

| Field | Type | Notes |
|---|---|---|
| `originalDriverId` | GUID | Driver who repeatedly breaks down |
| `replacementDriverId` | GUID | Driver who repeatedly replaces them |
| `swapCount` | int | Number of times this pair swapped (threshold: > 3) |
| `detectedAt` | datetime | UTC, when the pattern was detected |
| `isAcknowledged` | bool | Whether an Admin has reviewed this alert |

> **Detection rule:** The system runs hourly. If driver pair (A → B) has swapped more than 3 times, both drivers receive a −10 trust score penalty and this alert is created.

---

## 10. Real-time — SignalR Hub

**Endpoint:** `ws://localhost:8080/hubs/tracking`

**Auth:** Append JWT as query string: `?access_token=<jwt>`  
Or use `accessTokenFactory` in the SignalR client.

### Client SDK setup

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:8080/hubs/tracking", {
    accessTokenFactory: () => localStorage.getItem("accessToken")
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000])
  .configureLogging(signalR.LogLevel.Warning)
  .build();

await connection.start();
```

### Groups (subscribe after connecting)

| Group | Join method | Leave method | Who subscribes |
|---|---|---|---|
| `shipment:{shipmentId}` | `JoinShipmentGroup(shipmentId)` | `LeaveShipmentGroup(shipmentId)` | Customer tracking their order |
| `driver:{driverId}` | `JoinDriverGroup(driverId)` | `LeaveDriverGroup(driverId)` | Driver receiving assignments |
| `admin` | `JoinAdminGroup()` | `LeaveAdminGroup()` | Admin monitoring all activity |

### Server → Client events

#### `LocationUpdated`

Emitted when a driver pushes a GPS update.

```javascript
connection.on("LocationUpdated", (data) => {
  // Received by: shipment group + admin group
  console.log(data.driverId, data.latitude, data.longitude, data.recordedAt);
});
```

| Field | Type | Notes |
|---|---|---|
| `shipmentId` | GUID | |
| `driverId` | GUID | |
| `latitude` | double | |
| `longitude` | double | |
| `speedKmh` | double | |
| `recordedAt` | datetime | UTC |

#### `ShipmentStatusUpdated`

Emitted when shipment status changes.

```javascript
connection.on("ShipmentStatusUpdated", (data) => {
  // Received by: shipment group + admin group
  console.log(data.shipmentId, data.status, data.updatedAt);
});
```

| Field | Type | Notes |
|---|---|---|
| `shipmentId` | GUID | |
| `status` | string | New status |
| `updatedAt` | datetime | UTC |

#### `DriverAssigned`

Emitted when a driver is assigned to a shipment.

```javascript
connection.on("DriverAssigned", (data) => {
  // Received by: driver group
  showNewAssignmentNotification(data);
});
```

| Field | Type | Notes |
|---|---|---|
| `shipmentId` | GUID | |
| `orderId` | GUID | |
| `pickupAddress` | object | `{ street, city, province }` |
| `deliveryAddress` | object | `{ street, city, province }` |

#### `DispatcherConfirmationRequired`

Emitted when a shipment needs manual Admin approval (bin-check flagged).

```javascript
connection.on("DispatcherConfirmationRequired", (data) => {
  // Received by: admin group only
  addToReviewQueue(data);
});
```

| Field | Type | Notes |
|---|---|---|
| `shipmentId` | GUID | |
| `orderId` | GUID | |
| `reason` | string | Why manual review is needed |

### Full example (Customer)

```javascript
await connection.start();
await connection.invoke("JoinShipmentGroup", shipmentId);

connection.on("LocationUpdated", ({ latitude, longitude }) => {
  updateMapPin(latitude, longitude);
});

connection.on("ShipmentStatusUpdated", ({ status }) => {
  updateStatusBanner(status);
});

// Cleanup when leaving screen
window.addEventListener("beforeunload", async () => {
  await connection.invoke("LeaveShipmentGroup", shipmentId);
});
```

---

## 11. Enums Reference

### OrderStatus

| Value | Description |
|---|---|
| `Pending` | Order placed, awaiting confirmation |
| `Confirmed` | Confirmed, searching for driver |
| `AssignedToDriver` | Driver assigned |
| `PickedUp` | Cargo collected by driver |
| `InTransit` | En route to delivery |
| `Delivered` | Delivered successfully |
| `Cancelled` | Cancelled by customer or system |

### ShipmentStatus

| Value | Description |
|---|---|
| `Created` | Created from order event |
| `RoutePlanning` | Calculating route |
| `DriverAssigning` | Optimizing and assigning driver |
| `DriverConfirmed` | Driver confirmed |
| `DispatcherReviewRequired` | Awaiting manual Admin approval |
| `InProgress` | Driver picked up, in transit |
| `Reassigning` | Breakdown — finding new driver |
| `Completed` | Delivered |
| `Failed` | Failed after max retries |

### DriverStatus

| Value | Description |
|---|---|
| `Offline` | Not available (logged out or resting) |
| `Available` | Ready for assignment |
| `Busy` | Currently on a delivery |
| `Suspended` | Suspended by Admin |

### VehicleType

| Value | Description |
|---|---|
| `Motorbike` | Motorcycle, small parcels |
| `Van` | Van, light cargo |
| `Truck3T` | 3-tonne truck |
| `Truck5T` | 5-tonne truck |
| `Truck10T` | 10-tonne truck |
| `Truck15T` | 15-tonne truck |

### VehicleStatus

| Value | Description |
|---|---|
| `Available` | Ready for assignment |
| `InUse` | Currently assigned to a driver on a delivery |
| `Maintenance` | Under maintenance |
| `Breakdown` | Vehicle broke down (set automatically) |

### PaymentStatus

| Value | Description |
|---|---|
| `Created` | Payment record created |
| `Pending` | Awaiting processing |
| `Authorized` | Payment authorized |
| `Captured` | Funds captured |
| `Completed` | Payment complete |
| `Failed` | Payment failed |
| `Refunded` | Refunded |

### FraudRiskLevel

| Value | Description |
|---|---|
| `Unknown` | Risk level not yet assessed |
| `Low` | GPS within 2 km of last known position |
| `Medium` | GPS > 2 km from last known position |
| `High` | High confidence of fraud |
| `Confirmed` | Confirmed fraud |

---

## 12. Error Responses

### Standard error shape

```json
{
  "code": "ORDER_NOT_FOUND",
  "description": "Order with ID 550e8400... was not found"
}
```

### HTTP status codes

| Code | Meaning | Action |
|---|---|---|
| `200 OK` | Success | Process response |
| `201 Created` | Resource created | Use `Location` header or ID in body |
| `204 No Content` | Success, no body | Confirm action succeeded |
| `400 Bad Request` | Validation or business logic error | Show `description` to user |
| `401 Unauthorized` | Missing or expired token | Refresh token, then retry |
| `403 Forbidden` | Insufficient role | Show permission error |
| `404 Not Found` | Resource does not exist | Show not found message |
| `409 Conflict` | Duplicate resource or invalid state transition | Handle conflict (e.g., show "already exists") |
| `422 Unprocessable Entity` | Domain rule violation (e.g., fraud gate) | Show `description` |
| `429 Too Many Requests` | Rate limit hit (300 req/min per IP) | Back off and retry after 60s |
| `500 Internal Server Error` | Server error | Retry once; alert if persistent |
| `503 Service Unavailable` | Downstream service down | Retry with exponential backoff |

### Common error codes

| Code | Service | Cause |
|---|---|---|
| `USER_NOT_FOUND` | Identity | Email not registered |
| `INVALID_CREDENTIALS` | Identity | Wrong password |
| `ORDER_NOT_FOUND` | Order | Order ID does not exist |
| `INVALID_TRANSITION` | Order | Status change not allowed in current state |
| `DRIVER_NOT_FOUND` | Driver | Driver ID does not exist |
| `VEHICLE_ALREADY_ASSIGNED` | Driver | Vehicle in use by another driver |
| `FRAUD_GATE_REJECTED` | Driver | Breakdown report blocked by anti-fraud gate |
| `SHIPMENT_NOT_FOUND` | Shipment | Shipment ID does not exist |
| `PAYMENT_ALREADY_EXISTS` | Payment | Duplicate payment for order |
| `ESCROW_ALREADY_RESOLVED` | Payment | Escrow already confirmed or disputed |

---

## Rate Limits

| Scope | Limit |
|---|---|
| All endpoints | 300 requests/minute per IP |
| All endpoints | 5,000 requests/hour per IP |
| `POST /api/v1/tracking/location` | 120 req/min per authenticated user (JWT sub) |

---

## Phase 2+ Endpoints (Sprint 1–4 Additions)

### Driver — Extended Registration & Verification

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/drivers/register` | Driver JWT | Self-register (creates Driver + Vehicle, transitions to PendingOcrVerification) |
| `GET` | `/api/v1/drivers/pending-verification` | Admin | List drivers in PendingOcrVerification or ManualReview |
| `POST` | `/api/v1/drivers/{id}/verify` | Admin | Admin approve driver |
| `POST` | `/api/v1/drivers/{id}/reject-verification` | Admin | Admin reject driver with reason |
| `GET` | `/api/v1/drivers?status=&page=` | Admin | Paginated driver list with status filter |
| `POST` | `/api/v1/drivers/{id}/report-breakdown` | Driver | Report vehicle breakdown with photos + GPS |

**Self-register request body:**
```json
{
  "userId": "uuid",
  "fullName": "Nguyen Van A",
  "idCardNumber": "012345678901",
  "dateOfBirth": "1990-01-01",
  "licenseGrade": "C",
  "licenseExpiryDate": "2028-12-31",
  "vehicleLicensePlate": "51A-12345",
  "vehicleType": "Truck5T",
  "vehicleMaxWeightKg": 5000,
  "vehicleVolumeCbm": 20
}
```

### Vehicle

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/vehicles?status=&driverId=&type=&page=` | Admin | Paginated vehicle list |
| `PUT` | `/api/v1/vehicles/{id}/status` | Admin | Set vehicle status (`Available` or `Maintenance`) |

### File Uploads (MinIO pre-signed URLs)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/uploads/presigned-url?type=driver-document` | Driver | Get 7 PUT URLs for driver onboarding photos |
| `GET` | `/api/v1/uploads/presigned-url?type=breakdown-photo&count=3` | Driver | Get N PUT URLs for breakdown photos (1–10) |

Response shape: `{ "urls": ["https://minio/bucket/key?X-Amz-Signature=..."] }`

### Shipment — Admin Operations

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/shipments?status=&customerId=&driverId=&orderId=&page=` | Bearer | Paginated shipment list |
| `POST` | `/api/v1/shipments/{id}/confirm-dispatch` | Admin | Confirm dispatcher review → InProgress |
| `POST` | `/api/v1/shipments/{id}/decline-dispatch` | Admin | Decline dispatch → Failed |

### Payment — Extended

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/payments?status=&dateFrom=&dateTo=&page=` | Admin | Paginated payment list |
| `GET` | `/api/v1/payments/orders/{orderId}/escrow` | Bearer | Get escrow for an order (breakdown surcharge) |
| `POST` | `/api/v1/payments/orders/{orderId}/initiate` | Customer | Initiate VNPay payment, returns `paymentUrl` |
| `GET\|POST` | `/api/v1/payments/webhook/vnpay` | Public | VNPay callback (checksum verified server-side) |
| `POST` | `/api/v1/payments/escrow/{id}/confirm` | Customer/Admin | Release escrow |
| `POST` | `/api/v1/payments/escrow/{id}/dispute` | Customer/Admin | Dispute escrow |

### Notifications

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/notifications/register-device` | Bearer | Register FCM device token for push notifications |

**Register device request body:**
```json
{ "platform": "Android", "token": "fcm-device-token" }
```

### Analytics (Admin only)

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/analytics/kpis?days=30` | Admin | KPI summary (breakdown count, reassignment rate, etc.) |
| `GET` | `/api/v1/analytics/breakdown/incidents` | Admin | Breakdown incident list |
| `GET` | `/api/v1/analytics/fraud/alerts` | Admin | Fraud alert list |
| `POST` | `/api/v1/analytics/fraud/alerts/{id}/acknowledge` | Admin | Acknowledge a fraud alert |

### Identity — Admin

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/admin/accounts` | Admin | Create new Admin account |
