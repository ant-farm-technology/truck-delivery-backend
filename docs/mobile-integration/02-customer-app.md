# Customer App — Mobile Integration Guide

> Truck Delivery Backend · Tài liệu tích hợp cho ứng dụng mobile khách hàng
> Cập nhật: 2026-05-01 (Sprint 4)

---

## 1. Tổng quan kiến trúc

```
┌──────────────────────────────────────────────────────────────┐
│                    Customer App (Mobile)                     │
│                                                              │
│  [Auth] [Create Order] [Order List] [Tracking] [Payment]     │
└────────────────────────┬─────────────────────────────────────┘
                         │ HTTPS + JWT Bearer
                         │ X-Correlation-Id header
                         ▼
             ┌───────────────────────┐
             │  API Gateway — YARP   │
             │        :8080          │
             └───────────┬───────────┘
                         │ path prefix routing
        ┌────────────────┼───────────────────┐
        ▼                ▼                   ▼
  Identity :8081   Order :8082         Tracking :8087
  Payment  :8089   Shipment :8086      (SignalR /hubs/tracking)

[Push Notifications]  ← Notification Service :8088 → FCM
[Real-time tracking]  ← SignalR WebSocket (/hubs/tracking)
```

---

## 2. Luồng màn hình tổng quan

```
┌─────────────────────────────────────────────────────────────┐
│  ONBOARDING (1 lần)                                         │
│                                                             │
│  Đăng ký tài khoản (role=Customer) → Đăng nhập              │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  MAIN FLOW                                                  │
│                                                             │
│  Tạo đơn hàng (nhập địa chỉ + kiện hàng)                    │
│      ↓                                                      │
│  Theo dõi xử lý đơn (Pending → Confirmed → Assigned)        │
│      ↓                                                      │
│  Theo dõi tài xế real-time trên bản đồ                      │
│      ↓                                                      │
│  Nhận thông báo khi giao thành công                         │
│      ↓                                                      │
│  Xem hoá đơn thanh toán (COD / VNPay)                       │
└─────────────────────────────────────────────────────────────┘
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
  "dateOfBirth": "1990-01-15",
  "role": 1
}
```

`role`: Customer=1, Driver=2, Admin=3

```json
{
  "success": true,
  "data": { "userId": "550e8400-e29b-41d4-a716-446655440000" }
}
```

### 3.2 Đăng nhập

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "email": "customer@example.com",
  "password": "P@ssw0rd123"
}
```

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGci...",
    "refreshToken": "550e8400-...",
    "expiresIn": 3600,
    "role": "Customer",
    "userId": "550e8400-..."
  }
}
```

### 3.3 Refresh token

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "userId": "550e8400-...",
  "refreshToken": "550e8400-..."
}
```

Rotation được enforce: mỗi lần refresh → old token bị invalidate ngay. TTL refresh token: 30 ngày.

**Lưu ý:** Lưu cả `accessToken` và `refreshToken` vào secure storage (Keychain / Keystore). Auto-refresh khi còn 60 giây trước hết hạn.

### 3.4 Headers bắt buộc

```http
Authorization: Bearer <accessToken>
X-Correlation-Id: <uuid-v4>       (app tự sinh mỗi request hoặc per-session)
Content-Type: application/json
```

---

## 4. Tạo đơn hàng

### 4.1 Màn hình tạo đơn

```
┌──────────────────────────────────────────┐
│  Tạo đơn giao hàng                       │
│                                          │
│  Địa chỉ lấy hàng: [_________________]   │
│  Thành phố:        [_________________]   │
│  Tỉnh/TP:          [_________________]   │
│                                          │
│  Địa chỉ giao hàng: [________________]   │
│  Thành phố:         [________________]   │
│  Tỉnh/TP:           [________________]   │
│                                          │
│  ─── Kiện hàng ───────────────────────── │
│  Tên hàng:    [__________________]       │
│  Khối lượng:  [_____] kg                 │
│  Dài × Rộng × Cao: [__]×[__]×[__] m      │
│  Có thể nghiêng: [✓] / [✗]               │
│  Số lượng:    [___]                      │
│                                          │
│  [+ Thêm kiện]           [Tạo đơn hàng]  │
└──────────────────────────────────────────┘
```

### 4.2 API tạo đơn hàng

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
      "volumeCbm": 0.756,
      "lengthM": 0.6,
      "widthM": 0.7,
      "heightM": 1.8,
      "canTilt": false,
      "quantity": 1
    },
    {
      "productName": "Máy giặt LG",
      "weightKg": 60.0,
      "volumeCbm": 0.432,
      "lengthM": 0.6,
      "widthM": 0.6,
      "heightM": 1.2,
      "canTilt": false,
      "quantity": 2
    }
  ]
}
```

**Giải thích các trường kiện hàng:**

| Trường | Bắt buộc | Mô tả |
|---|---|---|
| `productName` | ✅ | Tên hàng hoá |
| `weightKg` | ✅ | Khối lượng (kg) |
| `volumeCbm` | ✅ | Thể tích (m³) — tính từ D×R×C |
| `lengthM` | Khuyến nghị | Chiều dài (m) — dùng cho bin-check |
| `widthM` | Khuyến nghị | Chiều rộng (m) — dùng cho bin-check |
| `heightM` | Khuyến nghị | Chiều cao (m) — dùng cho bin-check |
| `canTilt` | Khuyến nghị | Có thể đặt nghiêng không |
| `quantity` | ✅ | Số lượng |

> Nếu có `lengthM`, `widthM`, `heightM`, `canTilt` → hệ thống tự kiểm tra bin-pack (sắp xếp hàng lên xe). Nếu không có → dispatcher phải duyệt thủ công.

```json
{
  "success": true,
  "data": {
    "orderId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  },
  "meta": { "correlationId": "uuid" }
}
```

> **Lưu `orderId`** — dùng để theo dõi đơn hàng và truy vấn shipment.

### 4.3 volumeCbm tính tự động

```dart
// Tính volumeCbm từ dimensions
double calculateVolume(double length, double width, double height) {
  return length * width * height;
}
```

---

## 5. Danh sách đơn hàng

**Query params (tuỳ chọn):**

| Param | Mô tả | Ví dụ |
|---|---|---|
| `page` | Trang (default 1) | `?page=2` |
| `pageSize` | Số phần tử/trang (default 20) | `?pageSize=10` |
| `status` | Lọc theo trạng thái | `?status=InTransit` |
| `dateFrom` | Từ ngày (ISO 8601) | `?dateFrom=2026-04-01` |
| `dateTo` | Đến ngày (ISO 8601) | `?dateTo=2026-04-30` |

```http
GET /api/v1/orders?page=1&pageSize=20&status=InTransit
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": [
    {
      "id": "a1b2c3d4-...",
      "shipmentId": "c4d5e6f7-...",
      "status": "InTransit",
      "pickupAddress": { "city": "TP. Hồ Chí Minh" },
      "deliveryAddress": { "city": "Hà Nội" },
      "totalItems": 2,
      "createdAt": "2026-04-30T08:00:00Z"
    }
  ],
  "meta": {
    "correlationId": "uuid",
    "page": 1,
    "pageSize": 20,
    "total": 5
  }
}
```

**Màn hình danh sách:**

```
┌─────────────────────────────────────────┐
│  Đơn hàng của tôi                       │
│                                         │
│  ┌──────────────────────────────────┐   │
│  │ #a1b2c3d4... · 30/04/2026      │   │
│  │ HCM → Hà Nội · 2 kiện            │   │
│  │ 🚛 Đang trên đường giao đến bạn  │   │
│  │ [Theo dõi trực tiếp →]           │   │
│  └──────────────────────────────────┘   │
│                                         │
│  ┌──────────────────────────────────┐   │
│  │ #b2c3d4e5... · 28/04/2026      │   │
│  │ HCM → Đà Nẵng · 1 kiện           │   │
│  │ ✅ Đã giao thành công            │   │
│  └──────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

---

## 6. Chi tiết đơn hàng

### 6.1 Xem chi tiết đơn

```http
GET /api/v1/orders/{orderId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "a1b2c3d4-...",
    "shipmentId": "c4d5e6f7-...",
    "status": "InTransit",
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
        "quantity": 1
      }
    ],
    "createdAt": "2026-04-30T08:00:00Z",
    "updatedAt": "2026-04-30T10:30:00Z"
  }
}
```

### 6.2 Order Status (từ góc độ Customer)

```
Pending
  ↓ (hệ thống confirm tự động)
Confirmed
  ↓ (Saga tìm driver, bin-check OK)
AssignedToDriver  ←── Customer nhận push notification
  ↓ (driver đến lấy hàng)
PickedUp
  ↓ (driver bắt đầu chạy)
InTransit         ←── Real-time tracking bản đồ
  ↓ (driver giao xong, khách ký nhận)
Delivered         ←── Push notification + Payment tạo tự động
```

**Mapping hiển thị:**

| Status | Text hiển thị | Icon/màu |
|---|---|---|
| `Pending` | Đơn hàng đang chờ xác nhận | ⏳ Xám |
| `Confirmed` | Đã xác nhận, đang tìm tài xế | 🔵 Xanh dương |
| `AssignedToDriver` | Đã có tài xế, đang đến lấy hàng | 🟡 Vàng |
| `PickedUp` | Hàng đã được lấy | 🟠 Cam |
| `InTransit` | Đang trên đường giao đến bạn | 🚛 Xanh lá |
| `Delivered` | Đã giao thành công, chờ xác nhận thanh toán | ✅ Xanh lá đậm |
| `Completed` | Hoàn tất — thanh toán xác nhận xong | ✅✅ Xanh đậm |
| `Cancelled` | Đơn hàng đã bị huỷ | ❌ Đỏ |

### 6.3 Huỷ đơn hàng

```http
DELETE /api/v1/orders/{orderId}
Authorization: Bearer <token>
```

Chỉ huỷ được khi `status = Pending | Confirmed`. Response: `204 No Content`

**Validation trước khi gọi API:**

```dart
bool canCancelOrder(String status) {
  return status == 'Pending' || status == 'Confirmed';
}
```

---

## 7. Theo dõi Shipment (Tracking)

### 7.1 Lấy thông tin shipment từ orderId

Sau khi tạo đơn, hệ thống tự tạo shipment khi order được xử lý. App cần query để lấy `shipmentId`:

```http
GET /api/v1/shipments?orderId={orderId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "c4d5e6f7-...",
    "orderId": "a1b2c3d4-...",
    "status": "InProgress",
    "assignedDriverId": "7b2f4c8e-...",
    "assignedVehicleId": "a1b2c3d4-...",
    "createdAt": "2026-04-30T08:05:00Z"
  }
}
```

### 7.2 Lịch sử vị trí GPS

```http
GET /api/v1/tracking/shipments/{shipmentId}/points
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": [
    { "latitude": 10.7769, "longitude": 106.7009, "recordedAt": "2026-04-30T08:05:00Z" },
    { "latitude": 10.7812, "longitude": 106.6987, "recordedAt": "2026-04-30T08:06:00Z" },
    { "latitude": 10.7850, "longitude": 106.6940, "recordedAt": "2026-04-30T08:07:00Z" }
  ]
}
```

Dùng để vẽ polyline tuyến đường trên bản đồ.

### 7.3 Màn hình Tracking

```
┌──────────────────────────────────────────┐
│  Theo dõi đơn hàng                       │
│                                          │
│  ┌────────────────────────────────────┐ │
│  │         [BẢN ĐỒ]                   │ │
│  │                                    │ │
│  │  📍 Điểm lấy hàng                  │ │
│  │  🚛 Tài xế đang ở đây              │ │
│  │  📦 Điểm giao hàng                 │ │
│  │                                    │ │
│  └────────────────────────────────────┘ │
│                                          │
│  Trạng thái: Đang trên đường giao        │
│  Cập nhật lúc: 10:07:00                  │
│                                          │
│  ─── Timeline ────────────────────────── │
│  ✅ 08:00 Tạo đơn hàng                   │
│  ✅ 08:05 Tài xế được giao đơn           │
│  ✅ 09:30 Đã lấy hàng                    │
│  🔵 10:07 Đang giao hàng...              │
│  ⬜ Giao thành công                      │
└──────────────────────────────────────────┘
```

---

## 8. Real-time Tracking qua SignalR (`/hubs/tracking`)

### 8.1 Kết nối

```dart
// Flutter (signalr_netcore)
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

```swift
// iOS (Swift) — microsoft-signalr
let connection = HubConnectionBuilder(url: URL(string: "https://api.example.com/hubs/tracking")!)
  .withHttpConnectionOptions { opts in
    opts.accessTokenProvider = { [weak self] in self?.getAccessToken() }
  }
  .build()
try await connection.start()
```

### 8.2 Subscribe vào Shipment Group

```dart
// Join group để theo dõi shipment cụ thể
await connection.invoke("JoinShipmentGroup", args: [shipmentId]);

// Cleanup khi thoát màn hình
await connection.invoke("LeaveShipmentGroup", args: [shipmentId]);
```

### 8.3 Events nhận từ server

#### Cập nhật vị trí tài xế (real-time)

```dart
connection.on("LocationUpdated", (args) {
  // args[0]:
  // {
  //   "shipmentId": "c4d5e6f7-...",
  //   "driverId": "7b2f4c8e-...",
  //   "latitude": 10.7850,
  //   "longitude": 106.6940,
  //   "recordedAt": "2026-04-30T10:07:00Z"
  // }
  updateDriverMarkerOnMap(args[0]['latitude'], args[0]['longitude']);
  updateLastUpdatedTime(args[0]['recordedAt']);
});
```

#### Cập nhật trạng thái shipment

```dart
connection.on("ShipmentStatusUpdated", (args) {
  // args[0]:
  // {
  //   "shipmentId": "c4d5e6f7-...",
  //   "status": "Delivered",
  //   "updatedAt": "2026-04-30T15:30:00Z"
  // }
  updateStatusBanner(args[0]['status']);
  addTimelineEntry(args[0]);

  if (args[0]['status'] == 'Delivered') {
    stopTrackingAndShowCompletionScreen();
  }
});
```

### 8.4 Xử lý reconnect

```dart
connection.onreconnecting((error) {
  showReconnectingBanner("Đang kết nối lại...");
});

connection.onreconnected((connectionId) async {
  hideBanner();
  // Quan trọng: phải join lại group sau reconnect
  await connection.invoke("JoinShipmentGroup", args: [shipmentId]);
  // Reload lịch sử GPS để vẽ lại polyline
  await reloadTrackingPoints();
});

connection.onclose((error) {
  showOfflineBanner("Mất kết nối. Bản đồ không cập nhật.");
});
```

### 8.5 Lifecycle màn hình tracking

```dart
@override
void initState() {
  super.initState();
  _setupSignalR();
}

Future<void> _setupSignalR() async {
  await connection.start();
  await connection.invoke("JoinShipmentGroup", args: [widget.shipmentId]);
  // Load initial GPS points
  final points = await trackingApi.getPoints(widget.shipmentId);
  setState(() { _trackingPoints = points; });
}

@override
void dispose() {
  connection.invoke("LeaveShipmentGroup", args: [widget.shipmentId]);
  super.dispose();
}
```

---

## 9. Thanh toán

Hệ thống hỗ trợ 2 phương thức: **COD** (thanh toán tiền mặt khi nhận hàng) và **VNPay** (thanh toán online).

### 9.1 Xem thông tin thanh toán

```http
GET /api/v1/payments/orders/{orderId}
Authorization: Bearer <token>
```

```json
{
  "success": true,
  "data": {
    "id": "p1q2r3s4-...",
    "orderId": "a1b2c3d4-...",
    "status": "Completed",
    "amount": 350000,
    "method": "Cod",
    "completedAt": "2026-04-30T15:35:00Z"
  }
}
```

**`status` enum:**

| Status | Ý nghĩa | Hiển thị |
|---|---|---|
| `Created` | Đơn hàng vừa tạo, chưa tính phí | — |
| `Pending` | Chờ xử lý | "Đang xử lý" |
| `Authorized` | VNPay đã xác nhận, chờ capture | "Đang xử lý" |
| `Completed` | Thanh toán hoàn tất | "Đã thanh toán ✅" |
| `Failed` | Thất bại | "Thanh toán thất bại ❌" |
| `Refunded` | Đã hoàn tiền | "Đã hoàn tiền" |

**`method` enum:** `Cod` | `VnPay`

### 9.2 Luồng COD (thanh toán tiền mặt)

COD tự động complete khi driver set status `Delivered`. App không cần gọi thêm API — chỉ poll payment status hoặc nhận push `PAYMENT_COMPLETED`.

### 9.3 Luồng VNPay (thanh toán online)

```
1. Customer chọn "Thanh toán VNPay" → app gọi initiate API
2. Backend tạo Payment record + sinh URL redirect VNPay
3. App mở WebView / deep link đến paymentUrl
4. Customer thanh toán trên trang VNPay
5. VNPay callback về backend → backend verify HMAC → complete Payment
6. Customer nhận push notification "Thanh toán thành công"
```

**Bước 1: Khởi tạo thanh toán VNPay**

```http
POST /api/v1/payments/orders/{orderId}/initiate
Authorization: Bearer <token>     (role=Customer)
Content-Type: application/json

{
  "method": "VnPay"
}
```

```json
{
  "success": true,
  "data": {
    "paymentId": "p1q2r3s4-...",
    "paymentUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_..."
  }
}
```

**Bước 2: Mở WebView**

```dart
// Mở URL VNPay trong WebView
launchUrl(Uri.parse(data.paymentUrl), mode: LaunchMode.inAppWebView);

// Lắng nghe redirect về returnUrl (cấu hình trong appsettings.json: VnPay:ReturnUrl)
// VD: https://app.truckdelivery.vn/payment/result?vnp_ResponseCode=00&...
// vnp_ResponseCode == "00" → thành công
// vnp_ResponseCode != "00" → thất bại
```

**Bước 3: Sau khi WebView đóng, kiểm tra trạng thái**

```http
GET /api/v1/payments/orders/{orderId}
Authorization: Bearer <token>
```

Nếu `status == "Completed"` → hiển thị màn hình thành công. Nếu `Failed` → cho phép retry.

### 9.4 Màn hình hoá đơn

```
┌──────────────────────────────────────────┐
│  Hoá đơn giao hàng                       │
│                                          │
│  Đơn hàng #a1b2c3d4                      │
│  Ngày giao: 30/04/2026 15:30             │
│                                          │
│  ─── Chi tiết ────────────────────────── │
│  Tủ lạnh Samsung × 1                    │
│  Máy giặt LG × 2                        │
│  Tuyến: HCM → Hà Nội (~1.700 km)         │
│                                          │
│  ─── Thanh toán ───────────────────────  │
│  Phương thức: Tiền mặt (COD)             │
│       hoặc: VNPay                        │
│  Số tiền: 350.000 ₫                     │
│  Trạng thái: ✅ Đã thanh toán            │
│                                          │
│  [Tải hoá đơn PDF]   [Tạo đơn mới]      │
└──────────────────────────────────────────┘
```

---

## 10. Push Notifications (FCM)

### 10.1 Đăng ký FCM token

```http
POST /api/v1/notifications/register-device
Authorization: Bearer <token>

{
  "token": "fcm-device-token-here...",
  "platform": "Android"     // "Android" | "Ios"
}
```

### 10.2 Các notification customer nhận được

| Trigger | Notification | Action khi tap |
|---|---|---|
| Tài xế được assign | "Tài xế đang đến lấy hàng" | Mở màn hình tracking |
| Hàng đã lấy | "Tài xế đã lấy hàng, đang giao" | Mở màn hình tracking |
| Giao thành công | "Đơn hàng đã giao thành công!" | Mở màn hình hoá đơn |
| Thanh toán COD hoàn tất | "Thanh toán COD đã được xác nhận" | Mở màn hình hoá đơn |
| Thanh toán VNPay hoàn tất | "Thanh toán VNPay thành công" | Mở màn hình hoá đơn |
| Đơn bị huỷ | "Đơn hàng #... đã bị huỷ" | Mở màn hình chi tiết đơn |

### 10.3 Notification payload format

```json
{
  "notification": {
    "title": "Tài xế đang đến lấy hàng",
    "body": "Tài xế Trần Văn B đang trên đường đến 123 Nguyễn Huệ, TP.HCM"
  },
  "data": {
    "type": "DRIVER_ASSIGNED",
    "orderId": "a1b2c3d4-...",
    "shipmentId": "c4d5e6f7-..."
  }
}
```

**Các `type` values:**

| Type | Màn hình navigate |
|---|---|
| `DRIVER_ASSIGNED` | Tracking screen |
| `SHIPMENT_PICKED_UP` | Tracking screen |
| `SHIPMENT_DELIVERED` | Invoice screen |
| `PAYMENT_COMPLETED` | Invoice screen |
| `ORDER_CANCELLED` | Order detail screen |

---

## 11. Màn hình chủ (Home / Dashboard)

```
┌──────────────────────────────────────────┐
│  Xin chào, Nguyễn Văn A 👋               │
│                                          │
│  ─── Đơn đang giao ──────────────────── │
│  ┌──────────────────────────────────┐   │
│  │ 🚛 Đang giao · HCM → Hà Nội     │   │
│  │ Tài xế cách đây ~15 phút         │   │
│  │ [Xem bản đồ →]                   │   │
│  └──────────────────────────────────┘   │
│                                          │
│  ─── Đơn đang xử lý ─────────────────── │
│  ┌──────────────────────────────────┐   │
│  │ ⏳ Đang tìm tài xế · HCM → ĐN   │   │
│  │ Tạo lúc: 10:00 30/04/2026        │   │
│  └──────────────────────────────────┘   │
│                                          │
│  [+ Tạo đơn hàng mới]                   │
└──────────────────────────────────────────┘
```

**Logic hiển thị:**
- Lấy danh sách orders với status khác `Delivered` và `Cancelled`
- Sort by `createdAt` DESC
- Show "đang giao" card với link sang tracking nếu status = `InTransit` hoặc `PickedUp`

---

## 12. Offline & Resilience

| Tình huống | Xử lý đề xuất |
|---|---|
| Mất mạng khi xem tracking | Hiển thị banner "Offline — bản đồ không cập nhật"; show last known position |
| Token hết hạn | Auto-refresh silent; nếu refresh thất bại → redirect login |
| SignalR disconnect | `withAutomaticReconnect()` + rejoin group sau reconnect + reload GPS points |
| Tạo đơn thất bại | Hiển thị lỗi cụ thể (validation / server), cho phép retry |
| Huỷ đơn sau khi đã assigned | Show lỗi "Không thể huỷ đơn đang giao" |

---

## 13. Error Codes

| Code | HTTP | Ý nghĩa | Xử lý UI |
|---|---|---|---|
| `ORDER_NOT_FOUND` | 404 | orderId không tồn tại | Show lỗi, reload list |
| `ORDER_CANNOT_CANCEL` | 422 | Không thể huỷ ở trạng thái hiện tại | Show "Đơn đang giao, không thể huỷ" |
| `SHIPMENT_NOT_FOUND` | 404 | Shipment chưa tạo hoặc không tồn tại | Retry sau 3 giây |
| `VALIDATION_ERROR` | 400 | Dữ liệu gửi không hợp lệ | Highlight field lỗi |
| `UNAUTHORIZED` | 401 | Token hết hạn | Auto-refresh hoặc redirect login |
| `FORBIDDEN` | 403 | Không có quyền | Show lỗi rõ ràng |
| `DOMAIN_ERROR` | 422 | Lỗi business logic | Show message từ server |
| `SERVER_ERROR` | 500 | Lỗi nội bộ | "Hệ thống đang gặp sự cố, vui lòng thử lại" |

---

## 14. Polling vs Real-time Strategy

| Màn hình | Strategy | Lý do |
|---|---|---|
| Order detail | Polling mỗi 30s | Status thay đổi chậm |
| Tracking (tài xế di chuyển) | SignalR `LocationUpdated` | Real-time 1–5s |
| Home / Order list | Polling mỗi 60s hoặc pull-to-refresh | Không cần real-time |
| Payment | Event-driven (SignalR `ShipmentStatusUpdated`) | Trigger sau khi Delivered |

**Khi nào dừng SignalR subscription:**

```dart
// Dừng tracking khi shipment hoàn tất hoặc bị huỷ
connection.on("ShipmentStatusUpdated", (args) {
  final status = args[0]['status'];
  if (status == 'Completed' || status == 'Cancelled') {
    // Delivered → stay connected to receive Completed event (payment confirm)
    // Completed/Cancelled → leave group and navigate to invoice screen
    connection.invoke("LeaveShipmentGroup", args: [shipmentId]);
  }
  if (status == 'Delivered') {
    // Keep connection open — wait for Completed (payment) then show invoice
    showDeliveredBanner();
  }
});
```

---

## 15. Checklist tích hợp

- [ ] Implement auth flow (register → login → refresh token)
- [ ] Implement tạo đơn hàng với form nhập kiện hàng
- [ ] Tính `volumeCbm` tự động từ dimensions
- [ ] Implement danh sách đơn hàng (paginated)
- [ ] Implement chi tiết đơn + order status timeline
- [ ] Implement huỷ đơn với guard status check
- [ ] Implement tracking screen với bản đồ
- [ ] Load GPS history (polyline) khi mở tracking
- [ ] Connect SignalR `LocationUpdated` → update map marker
- [ ] Connect SignalR `ShipmentStatusUpdated` → update timeline
- [ ] Leave SignalR group khi thoát màn hình
- [ ] Implement invoice / payment screen (COD auto + VNPay WebView flow)
- [ ] Register FCM token sau login
- [ ] Handle notification tap → navigate to correct screen
- [ ] Handle token expiry (silent refresh)
- [ ] Handle SignalR reconnect + group rejoin
- [ ] Offline banner khi mất kết nối
