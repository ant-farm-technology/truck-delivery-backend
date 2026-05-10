# Customer App — Mobile Integration Guide

> Truck Delivery Backend · Tài liệu tích hợp cho ứng dụng mobile khách hàng
> Cập nhật: 2026-05-09 (audit từ source code thực tế)

---

## 1. Kiến trúc tổng quan

```
┌──────────────────────────────────────────────────────────────┐
│                    Customer App (Mobile)                     │
│  [Auth] [Estimate] [Create Order] [Orders] [Tracking] [Pay]  │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS + JWT Bearer + X-Correlation-Id
                         ▼
             ┌───────────────────────┐
             │  API Gateway — YARP   │  :8080
             └───────────┬───────────┘
        ┌────────────────┼─────────────────────┐
        ▼                ▼                     ▼
  Identity :8081   Order :8082          Tracking :8087
  Payment  :8089   Shipment :8086       SignalR /hubs/tracking
  Notification :8088
```

---

## 2. Luồng tổng quan

```
ONBOARDING
  POST /api/v1/auth/register          đăng ký
  POST /api/v1/auth/login             đăng nhập

MAIN FLOW
  GET  /api/v1/orders/pricing/estimate  (optional, không cần token)
  POST /api/v1/orders                   tạo đơn
  GET  /api/v1/orders?customerId=X      danh sách đơn
  GET  /api/v1/orders/{id}              chi tiết đơn (có shipmentId)
  GET  /api/v1/shipments/{id}           theo dõi shipment
  ← SignalR LocationUpdated event       real-time tracking
  GET  /api/v1/payments/orders/{id}     xem thanh toán
  POST /api/v1/payments/orders/{id}/initiate  khởi tạo VNPay
  POST /api/v1/payments/escrow/{id}/confirm   xác nhận nhận hàng (escrow)
```

---

## 3. Authentication

### 3.1 Đăng ký

```http
POST /api/v1/auth/register
Content-Type: application/json

{
  "email": "customer@example.com",
  "password": "P@ssw0rd123",
  "firstName": "Văn A",
  "lastName": "Nguyễn",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1990-01-15"
}
```

> `dateOfBirth` là optional cho Customer.

```json
{ "userId": "550e8400-e29b-41d4-a716-446655440000" }
```

### 3.2 Đăng nhập

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "email": "customer@example.com", "password": "P@ssw0rd123" }
```

```json
{
  "accessToken": "eyJhbGci...",
  "refreshToken": "abc123...",
  "expiresAt": "2026-06-01T10:30:00Z",
  "role": "Customer",
  "userId": "550e8400-..."
}
```

### 3.3 Refresh token

```http
POST /api/v1/auth/refresh

{ "userId": "550e8400-...", "refreshToken": "abc123..." }
```

Rotation enforced. TTL 30 ngày.

### 3.4 Logout

```http
POST /api/v1/auth/logout
Authorization: Bearer <token>
```

Response: `204 No Content`

### 3.5 Lấy profile tài khoản hiện tại

```http
GET /api/v1/auth/me
Authorization: Bearer <token>
```

```json
{
  "id": "550e8400-...",
  "email": "customer@example.com",
  "firstName": "Văn A",
  "lastName": "Nguyễn",
  "role": "Customer",
  "phoneNumber": "0901234567",
  "dateOfBirth": "1990-01-15",
  "isActive": true,
  "createdAt": "2026-04-30T08:00:00Z",
  "lastLoginAt": "2026-05-09T13:00:00Z"
}
```

### 3.6 Headers bắt buộc

```http
Authorization: Bearer <accessToken>
X-Correlation-Id: <uuid-v4>
Content-Type: application/json
```

---

## 4. Ước tính giá (không cần đăng nhập)

```http
GET /api/v1/orders/pricing/estimate
  ?vehicleType=Truck3T
  &pickupLat=10.7769&pickupLng=106.7009
  &deliveryLat=21.0278&deliveryLng=105.8342
  &weightKg=500
```

```json
{
  "vehicleType": "Truck3T",
  "distanceKm": 1730.5,
  "baseFee": 500000,
  "distanceFee": 3461000,
  "weightSurcharge": 0,
  "totalFee": 3961000,
  "currency": "VND"
}
```

`vehicleType`: `Motorbike`, `Van`, `Truck3T`, `Truck5T`, `Truck10T`, `Truck15T`

---

## 5. Tạo đơn hàng

```http
POST /api/v1/orders
Authorization: Bearer <token>
Content-Type: application/json

{
  "customerId": "550e8400-...",
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
      "quantity": 1,
      "weightKg": 45.0,
      "volumeCbm": 0.756,
      "lengthM": 0.6,
      "widthM": 0.7,
      "heightM": 1.8,
      "canTilt": false,
      "notes": null
    }
  ],
  "notes": null
}
```

| Field | Bắt buộc | Mô tả |
|---|---|---|
| `customerId` | ✅ | UserId từ login response |
| `productName` | ✅ | Tên hàng hoá |
| `quantity` | ✅ | Số lượng |
| `weightKg` | ✅ | Khối lượng |
| `volumeCbm` | ✅ | Thể tích m³ (= L×W×H) |
| `lengthM/widthM/heightM` | Khuyến nghị | Dùng cho bin-check tự động |
| `canTilt` | Khuyến nghị | Hàng có thể nghiêng không |

```json
{ "orderId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890" }
```

**Lưu `orderId`** — dùng để theo dõi và truy vấn.

---

## 6. Danh sách đơn hàng

```http
GET /api/v1/orders?customerId={customerId}&page=1&pageSize=20&status=InTransit
Authorization: Bearer <token>
```

**Query params:**

| Param | Mô tả |
|---|---|
| `customerId` | **Bắt buộc** — UserId của customer |
| `page` | Trang (default 1) |
| `pageSize` | Số phần tử/trang (default 20) |
| `status` | Lọc: `Pending`, `Confirmed`, `AssignedToDriver`, `PickedUp`, `InTransit`, `Delivered`, `Cancelled`, `Completed` |
| `dateFrom` / `dateTo` | ISO 8601 datetime |

```json
{
  "items": [
    {
      "id": "a1b2c3d4-...",
      "customerId": "550e8400-...",
      "status": "InTransit",
      "pickupCity": "TP. Hồ Chí Minh",
      "deliveryCity": "Hà Nội",
      "totalWeightKg": 45.0,
      "createdAt": "2026-04-30T08:00:00Z",
      "shipmentId": "c4d5e6f7-..."
    }
  ],
  "total": 5,
  "page": 1,
  "pageSize": 20
}
```

---

## 7. Chi tiết đơn hàng

```http
GET /api/v1/orders/{orderId}
Authorization: Bearer <token>
```

```json
{
  "id": "a1b2c3d4-...",
  "customerId": "550e8400-...",
  "status": "InTransit",
  "pickupStreet": "123 Nguyễn Huệ",
  "pickupCity": "TP. Hồ Chí Minh",
  "pickupProvince": "Hồ Chí Minh",
  "deliveryStreet": "456 Lê Lợi",
  "deliveryCity": "Hà Nội",
  "deliveryProvince": "Hà Nội",
  "totalWeightKg": 45.0,
  "totalVolumeCbm": 0.756,
  "notes": null,
  "cancellationReason": null,
  "createdAt": "2026-04-30T08:00:00Z",
  "updatedAt": "2026-04-30T10:30:00Z",
  "shipmentId": "c4d5e6f7-...",
  "items": [
    {
      "id": "item-uuid",
      "productName": "Tủ lạnh Samsung",
      "quantity": 1,
      "weightKg": 45.0,
      "volumeCbm": 0.756,
      "notes": null
    }
  ]
}
```

**OrderStatus — mapping hiển thị:**

| Status | Text hiển thị | Icon |
|---|---|---|
| `Pending` | Chờ xác nhận | ⏳ Xám |
| `Confirmed` | Đã xác nhận, đang tìm tài xế | 🔵 Xanh dương |
| `AssignedToDriver` | Tài xế đang đến lấy hàng | 🟡 Vàng |
| `PickedUp` | Hàng đã được lấy | 🟠 Cam |
| `InTransit` | Đang trên đường giao | 🚛 Xanh lá |
| `Delivered` | Đã giao, chờ xác nhận thanh toán | ✅ Xanh đậm |
| `Completed` | Hoàn tất | ✅✅ Xanh đậm |
| `Cancelled` | Đã huỷ | ❌ Đỏ |

---

## 8. Huỷ đơn hàng

```http
DELETE /api/v1/orders/{orderId}
Authorization: Bearer <token>

{ "reason": "Tôi muốn thay đổi địa chỉ giao hàng" }
```

Chỉ huỷ được khi `status = Pending | Confirmed`. Response: `204 No Content`

---

## 9. Theo dõi Shipment

### 9.1 Lấy thông tin shipment từ orderId

`shipmentId` có trong response của `GET /api/v1/orders/{orderId}` (`shipmentId` field). Hoặc query trực tiếp:

```http
GET /api/v1/shipments?orderId={orderId}
Authorization: Bearer <token>
```

```json
{
  "items": [
    {
      "id": "c4d5e6f7-...",
      "orderId": "a1b2c3d4-...",
      "customerId": "550e8400-...",
      "status": "InProgress",
      "pickupCity": "TP. Hồ Chí Minh",
      "pickupProvince": "Hồ Chí Minh",
      "deliveryCity": "Hà Nội",
      "deliveryProvince": "Hà Nội",
      "totalWeightKg": 45.0,
      "totalVolumeCbm": 0.756,
      "assignedDriverId": "7b2f4c8e-...",
      "assignedVehicleId": "a1b2c3d4-...",
      "distanceMeters": 1730000,
      "failureReason": null,
      "createdAt": "2026-04-30T08:00:00Z",
      "updatedAt": "2026-04-30T10:30:00Z"
    }
  ]
}
```

**ShipmentStatus:**

| Value | Ý nghĩa |
|---|---|
| `Created` | Vừa tạo |
| `RoutePlanning` | Đang tính tuyến đường |
| `DriverAssigning` | Đang tìm tài xế |
| `DriverConfirmed` | Tài xế đã xác nhận |
| `InProgress` | Đang giao |
| `Completed` | Giao thành công |
| `Failed` | Thất bại |
| `Reassigning` | Đang tìm tài xế thay thế (sau breakdown) |

### 9.2 Lịch sử GPS

```http
GET /api/v1/tracking/shipments/{shipmentId}/points?limit=100
Authorization: Bearer <token>
```

```json
[
  { "driverId": "...", "latitude": 10.7769, "longitude": 106.7009, "speedKmh": 45.5, "recordedAt": "..." }
]
```

---

## 10. Real-time Tracking qua SignalR

### 10.1 Kết nối

```dart
final connection = HubConnectionBuilder()
  .withUrl(
    "https://api.example.com/hubs/tracking",
    options: HttpConnectionOptions(
      accessTokenFactory: () async => await getAccessToken(),
    ),
  )
  .withAutomaticReconnect()
  .build();
await connection.start();
```

### 10.2 Join Shipment Group

```dart
await connection.invoke("JoinShipmentGroup", args: [shipmentId]);

// Cleanup khi thoát màn hình
await connection.invoke("LeaveShipmentGroup", args: [shipmentId]);
```

### 10.3 Event: LocationUpdated

```dart
connection.on("LocationUpdated", (args) {
  // args[0]: { "driverId": "...", "latitude": 10.785, "longitude": 106.694,
  //            "speedKmh": 52.3, "recordedAt": "..." }
  updateDriverMarkerOnMap(args[0]['latitude'], args[0]['longitude']);
});
```

> Không có `ShipmentStatusUpdated` qua SignalR — dùng polling `GET /orders/{id}` mỗi 30 giây.

### 10.4 Reconnect

```dart
connection.onreconnected((connectionId) async {
  await connection.invoke("JoinShipmentGroup", args: [shipmentId]);
  await reloadTrackingPoints();
});
```

---

## 11. Thanh toán

### 11.1 Xem thông tin thanh toán

```http
GET /api/v1/payments/orders/{orderId}
Authorization: Bearer <token>
```

```json
{
  "id": "p1q2r3s4-...",
  "orderId": "a1b2c3d4-...",
  "customerId": "550e8400-...",
  "amount": 3961000,
  "currency": "VND",
  "status": "Completed",
  "failureReason": null,
  "createdAt": "2026-04-30T08:00:00Z"
}
```

**PaymentStatus:**

| Status | Ý nghĩa | Hiển thị |
|---|---|---|
| `Created` | Vừa tạo | — |
| `Pending` | Chờ xử lý | "Đang xử lý" |
| `Authorized` | VNPay đã xác nhận | "Đang xử lý" |
| `Captured` | Đã capture | "Đang xử lý" |
| `Completed` | Hoàn tất | "Đã thanh toán ✅" |
| `Failed` | Thất bại | "Thất bại ❌" |
| `Refunded` | Đã hoàn tiền | "Đã hoàn tiền" |

### 11.2 COD (tiền mặt)

COD hoàn tất tự động khi driver set shipment = `Completed`. App chỉ cần poll `GET /payments/orders/{id}` hoặc nhận push `PAYMENT_COMPLETED`.

### 11.3 VNPay (thanh toán online)

**Bước 1: Khởi tạo**

```http
POST /api/v1/payments/orders/{orderId}/initiate
Authorization: Bearer <token>   (role=Customer)

{
  "customerId": "550e8400-...",
  "amount": 3961000,
  "method": "VnPay",
  "currency": "VND"
}
```

```json
{
  "paymentId": "p1q2r3s4-...",
  "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_..."
}
```

**Bước 2: Mở WebView**

```dart
launchUrl(Uri.parse(data['paymentUrl']), mode: LaunchMode.inAppWebView);
// Lắng nghe redirect về ReturnUrl (cấu hình trong appsettings: VnPay:ReturnUrl)
// vnp_ResponseCode == "00" → thành công
```

**Bước 3: Kiểm tra kết quả**

```http
GET /api/v1/payments/orders/{orderId}
```

### 11.4 Xác nhận/Khiếu nại nhận hàng (Escrow — chỉ khi driver bị swap)

Khi driver bị thay thế do breakdown, payment đi qua escrow. Customer cần xác nhận sau khi nhận hàng từ driver mới:

```http
GET /api/v1/payments/orders/{orderId}/escrow
Authorization: Bearer <token>
```

```json
{
  "id": "escrow-uuid",
  "shipmentId": "c4d5e6f7-...",
  "orderId": "a1b2c3d4-...",
  "originalDriverId": "...",
  "replacementDriverId": "...",
  "lockedAmount": 3961000,
  "currency": "VND",
  "status": "Locked",
  "resolutionNote": null,
  "lockedAt": "...",
  "resolvedAt": null
}
```

```http
POST /api/v1/payments/escrow/{escrowId}/confirm
Authorization: Bearer <token>   (role=Customer)

{ "note": "Đã nhận hàng đầy đủ" }
```

```http
POST /api/v1/payments/escrow/{escrowId}/dispute
Authorization: Bearer <token>   (role=Customer)

{ "note": "Hàng bị hư hỏng" }
```

Response: `204 No Content`

---

## 12. Push Notifications (FCM)

### 12.1 Đăng ký FCM token

```http
POST /api/v1/notifications/register-device
Authorization: Bearer <token>

{ "token": "fcm-device-token...", "platform": "Android" }
```

`platform`: `"Android"` | `"Ios"`. Response: `204 No Content`.

### 12.2 Notifications customer nhận được

| Trigger | Type | Hành động khi tap |
|---|---|---|
| Tài xế được assign | `DRIVER_ASSIGNED` | Mở tracking screen |
| Hàng đã lấy | `SHIPMENT_PICKED_UP` | Mở tracking screen |
| Giao thành công | `SHIPMENT_DELIVERED` | Mở payment screen |
| Thanh toán hoàn tất | `PAYMENT_COMPLETED` | Mở invoice screen |
| Đơn bị huỷ | `ORDER_CANCELLED` | Mở order detail |

---

## 13. Offline & Resilience

| Tình huống | Xử lý |
|---|---|
| Mất mạng khi xem tracking | Banner "Offline"; show last known position |
| Token hết hạn | Auto-refresh silent; nếu fail → redirect login |
| SignalR disconnect | `withAutomaticReconnect()` + rejoin group + reload GPS |
| Tạo đơn thất bại | Show lỗi + cho retry |
| Huỷ đơn sau Confirmed | Show "Không thể huỷ đơn đang giao" |

---

## 14. Error Codes

| Code | HTTP | Xử lý UI |
|---|---|---|
| `Order.NotFound` | 404 | Show lỗi, reload |
| `Order.CancelForbidden` | 400 | "Không thể huỷ đơn ở trạng thái này" |
| `Payment.Conflict` | 409 | Payment đã tồn tại cho đơn này |
| `Unauthorized` | 401 | Refresh token hoặc re-login |
