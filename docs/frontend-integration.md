# Frontend Integration Overview

> Truck Delivery Backend — Integration Guide for Frontend Teams
> Cập nhật: 2026-04-30

## Tài liệu chi tiết theo từng app

| App | Tài liệu chi tiết |
|---|---|
| Driver App (Mobile) | [docs/mobile-integration/01-driver-app.md](mobile-integration/01-driver-app.md) |
| Customer App (Mobile) | [docs/mobile-integration/02-customer-app.md](mobile-integration/02-customer-app.md) |

> Tài liệu này là **overview chung**. Xem các file trên để có hướng dẫn đầy đủ từng màn hình, API call, SignalR, và error handling.

---

---

## 1. Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────────┐
│                        FRONTENDS                            │
│                                                             │
│  [Customer App]    [Driver App]    [Admin Portal]           │
│  (Mobile/Web)      (Mobile)        (Web SPA)                │
└────────┬───────────────┬───────────────┬────────────────────┘
         │               │               │
         │   HTTPS + JWT (Bearer token)  │
         │   X-Correlation-Id header     │
         ▼               ▼               ▼
┌─────────────────────────────────────────────────────────────┐
│              API Gateway — YARP (:8080)                     │
│  Route matching → reverse proxy → downstream services       │
│  Injects X-Correlation-Id nếu client không gửi              │
└────────┬────────────────────────────────────────────────────┘
         │ routes by path prefix
         ├─ /api/v1/auth/*        → Identity   :8081
         ├─ /api/v1/orders/*      → Order      :8082
         ├─ /api/v1/drivers/*     → Driver     :8083
         ├─ /api/v1/vehicles/*    → Driver     :8083
         ├─ /api/v1/shipments/*   → Shipment   :8086
         ├─ /api/v1/tracking/*    → Tracking   :8087
         ├─ /api/v1/payments/*    → Payment    :8089
         └─ /hubs/tracking        → Tracking   :8087 (WebSocket/SignalR)
```

> Route, Optimizer là **internal services** — không expose qua API Gateway.
> Frontend KHÔNG gọi trực tiếp vào :8084 (Rust) hay :8085 (Python).

---

## 2. Authentication

### 2.1 Flow

```
1. POST /api/v1/auth/register  → đăng ký tài khoản
2. POST /api/v1/auth/login     → nhận access_token + refresh_token
3. Mọi request sau đó:        Authorization: Bearer <access_token>
4. Token hết hạn:             POST /api/v1/auth/refresh → access_token mới
```

### 2.2 Request/Response

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "driver@example.com",
  "password": "P@ssw0rd"
}
```

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGci...",
    "refreshToken": "550e8400-...",
    "expiresIn": 3600,
    "role": "Driver"
  }
}
```

### 2.3 Roles & Permissions

| Role | Value | Quyền hạn |
|---|---|---|
| `Customer` | 1 | Tạo đơn, xem đơn của mình, track shipment |
| `Driver` | 2 | Cập nhật trạng thái đơn, push GPS, báo hỏng xe |
| `Admin` | 3 | Toàn quyền: confirm dispatch, quản lý driver/vehicle, xem tất cả |

### 2.4 Headers bắt buộc

```http
Authorization: Bearer <access_token>
X-Correlation-Id: <uuid>          (optional nhưng nên gửi để debug)
Content-Type: application/json
```

---

## 3. Customer App

### 3.1 Luồng chính

```
Đăng ký/Đăng nhập
    ↓
Tạo đơn hàng (với kích thước kiện)
    ↓
Theo dõi shipment real-time (SignalR)
    ↓
Nhận thông báo push khi có cập nhật trạng thái
    ↓
Xác nhận nhận hàng → thanh toán COD
```

### 3.2 API Reference

#### Đăng ký

```http
POST /api/v1/auth/register
{
  "email": "customer@example.com",
  "password": "P@ssw0rd",
  "fullName": "Nguyen Van A",
  "phone": "0901234567",
  "role": "Customer"
}
```

#### Tạo đơn hàng

```http
POST /api/v1/orders
Authorization: Bearer <token>
{
  "pickupAddress": {
    "street": "123 Nguyễn Huệ",
    "city": "TP. Hồ Chí Minh",
    "province": "Hồ Chí Minh"
  },
  "deliveryAddress": {
    "street": "456 Lê Lợi",
    "city": "Hà Nội",
    "province": "Hà Nội"
  },
  "items": [
    {
      "productName": "Tủ lạnh Samsung",
      "weightKg": 45.0,
      "volumeCbm": 0.35,
      "lengthM": 0.6,
      "widthM": 0.7,
      "heightM": 1.8,
      "canTilt": false,
      "quantity": 1
    }
  ]
}
```

> `lengthM`, `widthM`, `heightM`, `canTilt` dùng cho bin-check — nếu không có thì dispatcher confirm thủ công.

```json
{
  "success": true,
  "data": { "orderId": "550e8400-..." }
}
```

#### Xem đơn hàng

```http
GET /api/v1/orders/{orderId}
GET /api/v1/orders?page=1&pageSize=20     (tất cả đơn của customer hiện tại)
```

#### Huỷ đơn

```http
DELETE /api/v1/orders/{orderId}
```

Chỉ huỷ được khi `status = Pending | Confirmed`.

#### Xem thông tin shipment

```http
GET /api/v1/shipments/{shipmentId}
```

```json
{
  "success": true,
  "data": {
    "id": "...",
    "orderId": "...",
    "status": "InProgress",
    "assignedDriverId": "...",
    "assignedVehicleId": "...",
    "createdAt": "2026-04-29T10:00:00Z"
  }
}
```

#### Xem lịch sử vị trí GPS

```http
GET /api/v1/tracking/shipments/{shipmentId}/points
```

```json
{
  "success": true,
  "data": [
    { "latitude": 10.7769, "longitude": 106.7009, "recordedAt": "2026-04-29T10:05:00Z" },
    { "latitude": 10.7812, "longitude": 106.6987, "recordedAt": "2026-04-29T10:06:00Z" }
  ]
}
```

#### Xem thông tin thanh toán

```http
GET /api/v1/payments/orders/{orderId}
```

### 3.3 Real-time Tracking (SignalR)

```javascript
// Kết nối
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://api.example.com/hubs/tracking", {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build();

// Nhận cập nhật vị trí tài xế
connection.on("LocationUpdated", (data) => {
  // data: { shipmentId, driverId, latitude, longitude, recordedAt }
  updateMapMarker(data.latitude, data.longitude);
});

// Nhận cập nhật trạng thái shipment
connection.on("ShipmentStatusUpdated", (data) => {
  // data: { shipmentId, status, updatedAt }
  updateStatusBanner(data.status);
});

// Subscribe vào shipment cụ thể sau khi kết nối
await connection.start();
await connection.invoke("JoinShipmentGroup", shipmentId);

// Cleanup khi thoát màn hình
await connection.invoke("LeaveShipmentGroup", shipmentId);
```

### 3.4 Order Status Machine (từ góc độ Customer)

```
Pending
  ↓ (hệ thống confirm)
Confirmed
  ↓ (driver được assign)
AssignedToDriver
  ↓ (driver pickup)
PickedUp → InTransit
  ↓ (driver giao xong)
Delivered
```

| Status | Hiển thị cho customer |
|---|---|
| `Pending` | "Đơn hàng đang chờ xác nhận" |
| `Confirmed` | "Đơn hàng đã được xác nhận, đang tìm tài xế" |
| `AssignedToDriver` | "Đã có tài xế, đang đến lấy hàng" |
| `PickedUp` | "Hàng đã được lấy, đang trên đường giao" |
| `InTransit` | "Hàng đang trên đường giao đến bạn" |
| `Delivered` | "Đã giao thành công" |
| `Cancelled` | "Đơn hàng đã bị huỷ" |

---

## 4. Driver App

### 4.1 Luồng chính

```
Đăng nhập (role=Driver)
    ↓
Set trạng thái Available
    ↓
Nhận assignment notification (push notification)
    ↓
Đến điểm pickup → cập nhật status = PickedUp
    ↓
Push GPS liên tục (interval 1–5 giây)
    ↓
Đến điểm delivery → cập nhật status = Delivered
    ↓
Set trạng thái Available (sẵn sàng chuyến tiếp)
```

### 4.2 API Reference

#### Cập nhật trạng thái Driver

```http
PUT /api/v1/drivers/{driverId}/status
Authorization: Bearer <token>
{
  "status": "Available"   // Offline | Available | Busy | Suspended
}
```

Tài xế phải set `Available` để được hệ thống tự động assign. Set `Offline` khi muốn nghỉ.

#### Xem thông tin bản thân

```http
GET /api/v1/drivers/{driverId}
```

```json
{
  "success": true,
  "data": {
    "id": "...",
    "fullName": "Tran Van B",
    "status": "Available",
    "assignedVehicle": {
      "id": "...",
      "licensePlate": "51A-12345",
      "type": "Truck3T",
      "maxWeightKg": 3000,
      "lengthM": 4.2,
      "widthM": 1.8,
      "heightM": 1.8
    }
  }
}
```

#### Push GPS location

```http
POST /api/v1/tracking/location
Authorization: Bearer <token>
{
  "shipmentId": "550e8400-...",
  "latitude": 10.7769,
  "longitude": 106.7009
}
```

Gọi mỗi **1–5 giây** khi đang chạy. App nên:
- Tăng interval lên 5s khi xe dừng > 30s (tiết kiệm battery)
- Giảm xuống 1s khi xe đang di chuyển
- Dừng push khi shipment = `Delivered` hoặc `Completed`

#### Cập nhật trạng thái Shipment

```http
PUT /api/v1/shipments/{shipmentId}/status
Authorization: Bearer <token>    (role=Driver)
{
  "status": "PickedUp"    // hoặc: InTransit | Delivered
}
```

| Action | Status cần set |
|---|---|
| Đã đến điểm lấy hàng và lấy xong | `PickedUp` |
| Đang chạy đường dài | `InTransit` |
| Đã giao hàng xong, khách ký nhận | `Delivered` |

#### Xem lịch sử tracking của shipment hiện tại

```http
GET /api/v1/tracking/shipments/{shipmentId}/points
```

### 4.3 Real-time: Nhận assignment

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://api.example.com/hubs/tracking", {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build();

// Nhận thông báo khi được assign shipment
connection.on("DriverAssigned", (data) => {
  // data: { shipmentId, orderId, pickupAddress, deliveryAddress, packages[] }
  showAssignmentDialog(data);
});

// Subscribe vào driver group
await connection.start();
await connection.invoke("JoinDriverGroup", driverId);
```

> **Lưu ý:** Assignment hiện tại là tự động (hệ thống Saga tự chọn). Driver không cần accept/reject. Push notification (FCM) được gửi kèm qua Notification Service.

### 4.4 Offline & Resilience

| Tình huống | Xử lý đề xuất |
|---|---|
| Mất mạng khi đang push GPS | Cache location locally, gửi batch khi có mạng lại |
| Token hết hạn | Auto refresh trước khi hết hạn 60s; nếu refresh thất bại → redirect login |
| SignalR disconnect | `withAutomaticReconnect()` tự reconnect; rejoin group sau khi reconnect |
| Shipment status update thất bại | Retry 3 lần với exponential backoff |

---

## 5. Admin Portal

### 5.1 Chức năng chính

| Module | Mô tả |
|---|---|
| **Driver Management** | Tạo tài xế, assign xe, xem danh sách, đổi trạng thái |
| **Vehicle Management** | Thêm xe, xem thông số, set maintenance |
| **Shipment Monitor** | Xem tất cả shipment, filter theo status |
| **Dispatch Review** | Duyệt các shipment cần xác nhận thủ công (bin-check failed) |
| **Live Tracking** | Xem map tất cả xe đang chạy |
| **Payment Overview** | Xem thanh toán, trạng thái COD |

### 5.2 API Reference

#### Quản lý Driver

```http
POST /api/v1/drivers               (Admin) — tạo tài xế mới
GET  /api/v1/drivers/{id}          (Bearer) — xem chi tiết
GET  /api/v1/drivers/available     (Bearer) — danh sách đang Available
PUT  /api/v1/drivers/{id}/status   (Bearer) — đổi trạng thái
```

#### Quản lý Vehicle

```http
POST /api/v1/vehicles                        (Admin) — thêm xe mới
GET  /api/v1/vehicles/{id}                   (Bearer) — xem chi tiết
POST /api/v1/drivers/{id}/assign-vehicle     (Admin) — gán xe cho tài xế
```

Payload tạo vehicle:
```json
{
  "licensePlate": "51A-12345",
  "type": "Truck3T",
  "maxWeightKg": 3000,
  "lengthM": 4.2,
  "widthM": 1.8,
  "heightM": 1.8
}
```

`type` enum: `Motorbike | Van | Truck3T | Truck5T | Truck10T | Truck15T`

#### Duyệt Dispatch thủ công

Khi bin-check trả về `requires_dispatcher_confirmation = true`, shipment chuyển sang status `DispatcherReviewRequired`. Admin phải duyệt:

```http
POST /api/v1/shipments/{shipmentId}/confirm-dispatch
Authorization: Bearer <token>    (role=Admin)
```

Response: `204 No Content`

> Sau khi confirm, shipment chuyển sang `InProgress` và driver nhận thông báo.

#### Xem Shipment

```http
GET /api/v1/shipments/{shipmentId}         (Bearer)
```

Để lấy danh sách (query endpoint chưa implement — gọi qua query repository trực tiếp trong internal admin hoặc sẽ bổ sung).

#### Xem Payments

```http
GET /api/v1/payments/orders/{orderId}      (Bearer)
```

### 5.3 Dispatch Review Queue

Admin cần polling hoặc subscribe event để biết khi nào có shipment cần duyệt:

**Option A — Polling:**
```javascript
// Poll mỗi 30 giây để kiểm tra shipment cần duyệt
setInterval(async () => {
  const pending = await fetch('/api/v1/shipments?status=DispatcherReviewRequired', {
    headers: { Authorization: `Bearer ${token}` }
  });
  updateReviewQueue(await pending.json());
}, 30_000);
```

**Option B — SignalR (recommended):**
```javascript
connection.on("DispatcherConfirmationRequired", (data) => {
  // data: { shipmentId, orderId, reason, packages[], binCheckWarnings[] }
  addToReviewQueue(data);
});

// Subscribe vào admin group
await connection.invoke("JoinAdminGroup");
```

### 5.4 Live Tracking Map

```javascript
// Subscribe tất cả location updates (admin only)
connection.on("LocationUpdated", (data) => {
  // data: { shipmentId, driverId, latitude, longitude, recordedAt }
  updateMapMarker(data.driverId, data.latitude, data.longitude);
});

await connection.invoke("JoinAdminGroup");
```

---

## 6. SignalR Hub Reference (`/hubs/tracking`)

### Groups

| Group name | Join method | Ai subscribe |
|---|---|---|
| `shipment:{shipmentId}` | `JoinShipmentGroup(shipmentId)` | Customer (track đơn của mình) |
| `driver:{driverId}` | `JoinDriverGroup(driverId)` | Driver (nhận assignment) |
| `admin` | `JoinAdminGroup()` | Admin (tất cả events) |

### Server → Client events

| Event | Payload | Ai nhận |
|---|---|---|
| `LocationUpdated` | `{ shipmentId, driverId, latitude, longitude, recordedAt }` | Customer (shipment group), Admin |
| `ShipmentStatusUpdated` | `{ shipmentId, status, updatedAt }` | Customer (shipment group), Admin |
| `DriverAssigned` | `{ shipmentId, orderId, pickupAddress, deliveryAddress, packages[] }` | Driver (driver group) |
| `DispatcherConfirmationRequired` | `{ shipmentId, orderId, reason, binCheckWarnings[] }` | Admin |

### Authentication

SignalR dùng cùng JWT Bearer token:
```
wss://api.example.com/hubs/tracking?access_token=<jwt>
```

Hoặc qua `accessTokenFactory` như ví dụ trên.

---

## 7. Notification (Push / SMS)

Notification Service tự động gửi khi các events sau xảy ra:

| Event | Kênh | Người nhận |
|---|---|---|
| Driver assigned | Push + SMS | Customer |
| Shipment picked up | Push | Customer |
| Shipment delivered | Push + SMS | Customer |
| Payment completed | Push | Customer |
| New assignment | Push | Driver |

Frontend cần:
1. Đăng ký FCM token sau khi login
2. Gửi FCM token lên backend (endpoint sẽ bổ sung vào Notification Service)
3. Xử lý foreground notification khi app đang mở

---

## 8. Error Handling

### Response format chuẩn

```json
// Success
{
  "success": true,
  "data": { ... },
  "error": null,
  "meta": { "correlationId": "uuid" }
}

// Error
{
  "success": false,
  "data": null,
  "error": {
    "code": "ORDER_NOT_FOUND",
    "message": "Không tìm thấy đơn hàng"
  },
  "meta": { "correlationId": "uuid" }
}
```

### HTTP Status Codes

| Code | Xử lý ở client |
|---|---|
| `200 / 201 / 204` | Success |
| `400` | Validation error — hiển thị `error.message` cho user |
| `401` | Token hết hạn → refresh, nếu không được → logout |
| `403` | Không đủ quyền — hiển thị thông báo |
| `404` | Không tìm thấy — điều hướng về danh sách |
| `409` | Conflict (duplicate) — thông báo trùng |
| `422` | Domain error — hiển thị `error.message` |
| `500` | Lỗi server — retry 1 lần, nếu vẫn lỗi thì báo user |
| `503` | Service không khả dụng — retry với backoff |

---

## 9. Rate Limits & Best Practices

| Endpoint | Khuyến nghị |
|---|---|
| `POST /api/v1/tracking/location` | Tối đa 1 req/giây; giảm xuống 1 req/5 giây khi xe dừng |
| `GET /api/v1/orders` (polling) | Không nên poll < 10 giây; dùng SignalR thay thế |
| `POST /api/v1/auth/refresh` | Chỉ gọi khi token sắp hết hạn (còn < 60s) |
| Batch GPS (offline recovery) | Gửi tối đa 50 điểm/request khi online lại |

---

## 10. Environment & Base URLs

| Environment | Base URL |
|---|---|
| Development | `http://localhost:8080` |
| Staging | `https://staging-api.truckdelivery.example.com` |
| Production | `https://api.truckdelivery.example.com` |

> Tất cả requests đều đi qua API Gateway — frontend **không cần biết** port của từng microservice.

---

## 11. Tóm tắt Quick Reference

| App | Login Role | Endpoints chính | Real-time |
|---|---|---|---|
| **Customer App** | `Customer` | `/orders`, `/shipments/{id}`, `/tracking/shipments/{id}/points` | Subscribe `shipment:{id}` group |
| **Driver App** | `Driver` | `/drivers/{id}/status`, `/tracking/location`, `/shipments/{id}/status` | Subscribe `driver:{id}` group |
| **Admin Portal** | `Admin` | `/drivers`, `/vehicles`, `/shipments/{id}/confirm-dispatch`, `/payments` | Subscribe `admin` group |
