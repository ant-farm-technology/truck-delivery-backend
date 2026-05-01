# API Business Gap Analysis

> Truck Delivery Backend — Đánh giá nghiệp vụ cho tích hợp client
> Phân tích: 2026-04-30 | **Cập nhật status: 2026-05-01 (Sprint 1–4 + Doc Sprint hoàn thành)**
> Dựa trên khảo sát toàn bộ controllers, DTOs, domain aggregates, consumers, Saga flows

---

## Tóm tắt điều hành

Hệ thống có backbone kỹ thuật tốt (Saga, Kafka, Outbox, OpenTelemetry). Sau Sprint 1–4 + Doc Sprint, **toàn bộ 21 gaps đã được giải quyết**.

| Mức độ | Số lượng ban đầu | Đã fix | Còn lại |
|---|---|---|---|
| 🔴 **Blocker** | 6 | 6 | 0 |
| 🟡 **High** | 11 | 11 | 0 |
| 🟢 **Medium** | 4 | 4 | 0 |

---

## 1. Customer App — 7 Gaps

### C1 ✅ FIXED — `shipmentId` trong `OrderDto`

**Giải pháp đã implement:** `OrderDto` và `OrderSummaryDto` đều có `ShipmentId: Guid?`. Dapper queries trong `GetOrderByIdQueryHandler` và `ListOrdersByCustomerQueryHandler` đều select `o.ShipmentId`.

~~**Vấn đề:**
Customer tạo order → nhận được `orderId`. Nhưng để track real-time qua SignalR, client cần join group `shipment:{shipmentId}`. Hiện tại không có cách nào để resolve `orderId` → `shipmentId`.

~~**Ảnh hưởng:** Customer không thể theo dõi đơn hàng real-time.~~

~~**Giải pháp:** Đã implement — `ShipmentId` có trong `OrderDto` và `OrderSummaryDto`.~~

---

### C2 ✅ FIXED — `GET /api/v1/payments/orders/{orderId}/escrow`

**Giải pháp đã implement:** `GetEscrowByOrderQuery` + `GetEscrowByOrderQueryHandler` (Dapper). `PaymentsController` expose `GET /api/v1/payments/orders/{orderId}/escrow` → trả về `EscrowDto`.

~~**Vấn đề:** Không có cách nào biết `escrowId`.~~

**Ảnh hưởng:** Customer không thể confirm hay dispute escrow. Số tiền bị lock vô thời hạn.

**Giải pháp đề xuất:** Thêm endpoint `GET /api/v1/payments/orders/{orderId}/escrow` trả về danh sách escrow (nếu có) cho một order.

---

### C3 🟡 Driver onboarding flow bị đứt

**Vấn đề:**
`POST /api/v1/auth/register` nhận `{ email, password, firstName, lastName }` — **không có field `role`**. Default là `Customer`. 

Để tạo Driver profile, phải gọi thêm `POST /api/v1/drivers` — nhưng endpoint này yêu cầu role `Admin`. Tức là:
- Driver tự đăng ký → được tạo user với role Customer
- Không thể tự tạo Driver profile
- Phải nhờ Admin tạo driver profile thủ công

**Ảnh hưởng:** Không có luồng self-service cho Driver. Mọi tài xế đều cần Admin can thiệp thủ công.

**Giải pháp đề xuất:**
- **A:** Thêm field `role` vào `RegisterRequest` (nhưng cần validation — không cho client tự set Admin).
- **B:** Tạo endpoint `POST /api/v1/drivers/register` không cần Admin role — driver tự đăng ký kèm thông tin license, phone.

---

### C4 🟡 List orders thiếu filter và pagination metadata

**Vấn đề:**
```
GET /api/v1/orders?customerId={id}&page=1&pageSize=20
```

- Chỉ filter được theo `customerId`, không có `status`, `dateFrom`, `dateTo`
- Response trả về array thuần, **không có** `totalCount`, `totalPages`, `hasNextPage`
- UI không thể render pagination controls, không thể biết còn bao nhiêu trang

**Ảnh hưởng:** List đơn hàng không usable trong thực tế khi customer có nhiều đơn.

**Giải pháp đề xuất:** Wrap response thành:
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```
Thêm query params: `status`, `dateFrom`, `dateTo`.

---

### C5 🔴 Order status không sync khi Shipment hoàn thành

**Vấn đề:**
Khi Driver gọi `PUT /shipments/{id}/status` với `status = Delivered`:
1. Shipment → `Completed` ✅
2. Payment tự động tạo ✅ (via `ShipmentCompletedEvent` → `OrderDeliveredConsumer`)
3. **Order status vẫn là `InTransit` ❌** — Order service không consume `ShipmentCompletedEvent`

**Ảnh hưởng:** Customer thấy Order mãi ở trạng thái `InTransit` dù đã giao xong. Trạng thái không đáng tin cậy.

**Nguyên nhân:** Order service chỉ consume `userregistered` topic (theo code). Không có consumer nào cho `shipment.shipment.completed`.

**Giải pháp đề xuất:** Thêm `ShipmentCompletedConsumer` trong Order service, consume topic `shipment.shipment.completed`, gọi `UpdateOrderStatusCommand` để set Order → `Delivered`.

---

### C6 🟡 Không có FCM token registration

**Vấn đề:**
Notification service hiện là pure event-driven, không có REST API. Không có endpoint nào để client gửi FCM device token. `StubPushSender` chỉ log ra console.

**Ảnh hưởng:** Push notification không thể đến thiết bị thực. Mọi notification đang bị drop silently.

**Giải pháp đề xuất:** Thêm endpoint trong Notification service (hoặc Identity service):
```
POST /api/v1/notifications/device-tokens
{ "platform": "android|ios", "token": "fcm-token-here" }
```
Lưu token gắn với `userId` từ JWT.

---

### C7 🟡 Không có endpoint list shipments của customer

**Vấn đề:**
Customer chỉ có thể:
- Xem đơn hàng: `GET /orders` (có)
- Xem shipment theo ID: `GET /shipments/{id}` (có)

Nhưng không có cách nào list tất cả shipments của mình. Sau khi order được dispatch, customer muốn xem "tất cả đơn đang giao" phải tự nhớ từng `shipmentId`.

**Giải pháp đề xuất:** `GET /api/v1/shipments?customerId={id}&status=&page=`

---

## 2. Driver App — 6 Gaps

### D1 🔴 Không có "shipment hiện tại của tôi"

**Vấn đề:**
Driver không thể query "tôi đang phụ trách shipment nào?". Khi Driver app khởi động lại (crash, restart), driver mất context về shipment ID hiện tại.

**Ảnh hưởng:** Driver app không thể resume session sau crash. Driver phải được admin báo shipment ID thủ công.

**Giải pháp đề xuất:** Thêm endpoint:
```
GET /api/v1/shipments/active?driverId={id}
```
hoặc:
```
GET /api/v1/drivers/{id}/active-shipment
```
Trả về shipment đang `InProgress` hoặc `Reassigning` của driver.

---

### D2 🟡 GPS push thiếu `shipmentId`

**Vấn đề:**
```http
POST /api/v1/tracking/location
{ "latitude": ..., "longitude": ..., "speedKmh": ... }
```

Driver ID được lấy từ JWT `sub`. Tracking service phải tự tìm shipment nào đang active cho driver này để broadcast SignalR event đúng group.

Nếu driver có thể phụ trách nhiều delivery trong tương lai, hoặc khi đang trong trạng thái `Reassigning` (2 shipments liên quan), logic này sẽ bị nhầm.

**Giải pháp đề xuất:** Thêm optional `shipmentId` vào request body để client explicit:
```json
{ "shipmentId": "...", "latitude": ..., "longitude": ... }
```

---

### D3 🟡 Không có real-time notification khi được assign

**Vấn đề:**
Khi driver được assign một shipment:
1. Notification service gửi Push/SMS (stub — không thực sự gửi được)
2. **Không có SignalR event** nào được push đến `driver:{driverId}` group

**Ảnh hưởng:** Nếu Push notification không hoạt động (stub), driver không biết mình được assign. Phải poll API thủ công.

**Giải pháp đề xuất:** Tracking service (đã có SignalR hub) nên emit event `DriverAssigned` đến group `driver:{driverId}` khi nhận `ShipmentStartedEvent` từ Kafka.

---

### D4 🟢 Không có lịch sử chuyến của driver

**Vấn đề:** Driver không thể xem lịch sử các shipment đã hoàn thành của mình.

**Giải pháp đề xuất:** `GET /api/v1/shipments?driverId={id}&status=Completed&page=`

---

### D5 🟡 Breakdown photos — không có upload endpoint

**Vấn đề:**
```json
POST /api/v1/drivers/{id}/report-breakdown
{
  "photoUrls": ["https://..."]
}
```

Anti-fraud gate yêu cầu ít nhất 1 photo. Nhưng không có endpoint nào để upload ảnh. Driver phải upload lên dịch vụ bên ngoài (S3, Cloudinary) và paste URL vào request.

**Ảnh hưởng:** Trong môi trường mobile app thực tế, driver không thể chụp ảnh và báo hỏng xe trong một luồng liền mạch. Phải có bước trung gian phức tạp.

**Giải pháp đề xuất (2 phương án):**
- **A (đơn giản):** Thêm `POST /api/v1/drivers/breakdown-photos/upload` — multipart upload, trả về URL.
- **B (scalable):** Tích hợp pre-signed S3 URL — `GET /api/v1/uploads/presigned-url?type=breakdown-photo` trả về URL và field để client upload trực tiếp lên S3.

---

### D6 🟢 TrustScore không visible trong DriverDto

**Vấn đề:**
`DriverDto` trả về: `Id, Email, FirstName, LastName, PhoneNumber, LicenseNumber, Status, CurrentVehicleId, IsActive, CreatedAt`.

`TrustScore` (0–100, ảnh hưởng đến khả năng báo hỏng xe) không được expose. Driver không biết điểm của mình, không biết tại sao bị reject `report-breakdown`.

**Giải pháp đề xuất:** Thêm `TrustScore: int` vào `DriverDto`. Endpoint `GET /drivers/{id}` trả về trust score cho chính driver đó (ẩn với người khác trừ Admin).

---

## 3. Admin Portal — 8 Gaps

### A1 🔴 Không có list shipments

**Vấn đề:**
Chỉ có `GET /api/v1/shipments/{id}`. Admin phải biết exact shipment ID trước mới xem được.

**Ảnh hưởng:** Admin không thể:
- Xem queue shipment đang chờ `DispatcherReviewRequired`
- Monitor shipments đang `Reassigning` (breakdown)
- Xem tổng quan fleet đang hoạt động

**Giải pháp đề xuất:**
```
GET /api/v1/shipments?status=DispatcherReviewRequired&page=1&pageSize=20
GET /api/v1/shipments?status=InProgress
GET /api/v1/shipments?status=Reassigning
```

---

### A2 🟡 Không có list payments

**Vấn đề:** `GET /api/v1/payments/orders/{orderId}` — chỉ lookup theo orderId. Không có cách nào xem tổng quan thanh toán, filter theo ngày, status, hay tổng doanh thu.

**Giải pháp đề xuất:** `GET /api/v1/payments?status=&dateFrom=&dateTo=&page=`

---

### A3 🟡 Không có list tất cả drivers

**Vấn đề:** Chỉ có `GET /api/v1/drivers/available`. Admin không thể xem drivers đang `Offline`, `Busy`, `Suspended`, hay tất cả drivers trong fleet.

**Giải pháp đề xuất:** `GET /api/v1/drivers?status=&page=` (không truyền status = lấy tất cả)

---

### A4 🟡 Không có list vehicles

**Vấn đề:** `GET /api/v1/vehicles/{id}` — chỉ lookup theo ID. Không thể xem tổng quan fleet xe.

**Giải pháp đề xuất:** `GET /api/v1/vehicles?status=&driverId=&type=&page=`

---

### A5 🔴 Analytics service không expose qua API Gateway

**Vấn đề:**
Analytics service chạy trên `:8095`. API Gateway (`:8080`) **không có route** nào cho `/api/v1/analytics/*`.

**Kiểm tra gateway config (`appsettings.json`):**
- Routes được định nghĩa: identity, order, driver, shipment, tracking, notification, payment, route-service, optimizer
- **Không có** `analytics-route` hay `analytics-cluster`

**Ảnh hưởng:** Admin Portal phải gọi thẳng port `:8095`, bypass JWT validation của Gateway. Không an toàn và không thực tế khi deploy.

**Giải pháp đề xuất:** Thêm vào Gateway `appsettings.json`:
```json
"analytics-route": {
  "ClusterId": "analytics-cluster",
  "AuthorizationPolicy": "default",
  "Match": { "Path": "/api/v1/analytics/{**catch-all}" }
},
"analytics-cluster": {
  "Destinations": {
    "primary": { "Address": "http://analytics-service:8095/" }
  }
}
```

---

### A6 🟢 Driver suspend không có role guard

**Vấn đề:**
`PUT /api/v1/drivers/{id}/status` có `[Authorize]` (bất kỳ user nào có JWT) nhưng **không** `[Authorize(Roles = "Admin")]`. Bất kỳ Customer hay Driver nào cũng có thể gọi endpoint này và thay đổi status của driver khác.

Thêm vào đó, không có endpoint semantic rõ ràng cho các action:
- Suspend driver (vi phạm)
- Deactivate driver (nghỉ việc)
- Reactivate driver

Tất cả đang dùng chung `PUT /status` với string enum.

**Giải pháp đề xuất:**
1. Thêm `[Authorize(Roles = "Admin")]` cho `PUT /drivers/{id}/status`
2. Xem xét tạo endpoint riêng cho `suspend` nếu cần audit trail

---

### A7 🟡 Không có acknowledge fraud alert

**Vấn đề:**
`GET /api/v1/analytics/fraud/alerts` trả về `isAcknowledged: bool`. Nhưng không có endpoint để Admin mark một alert là "đã xem xét / đã xử lý".

**Ảnh hưởng:** Alert queue mãi hiển thị cùng một set alerts cũ. Admin không có workflow xử lý.

**Giải pháp đề xuất:**
```
POST /api/v1/analytics/fraud/alerts/{id}/acknowledge
```

---

### A8 🟢 Không có endpoint set vehicle maintenance

**Vấn đề:**
`VehicleStatus` có giá trị `Maintenance` nhưng không có endpoint `PUT /api/v1/vehicles/{id}/status`. Admin không thể đánh dấu xe đang bảo dưỡng qua API.

**Giải pháp đề xuất:** `PUT /api/v1/vehicles/{id}/status` với `{ "status": "Available|Maintenance" }` (Admin only)

---

## 4. Cross-cutting Issues

### X1 🟡 Pagination thiếu metadata trên tất cả list endpoints

**Vấn đề:**
Tất cả list endpoints (`GET /orders`, v.v.) trả về array thuần, không có:
- `totalCount` — tổng số records
- `totalPages` — tổng số trang
- `hasNextPage` — còn trang tiếp không
- `currentPage`, `pageSize`

**Ảnh hưởng:** Frontend không thể render pagination UI. Load more / infinite scroll không khả thi.

**Giải pháp đề xuất:** Chuẩn hóa tất cả list response thành `PagedResult<T>`:
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true
}
```

---

### X2 🟡 `PUT /shipments/{id}/status` — Driver không bị giới hạn status

**Vấn đề:**
Controller chỉ check `[Authorize(Roles = "Admin,Driver")]` nhưng không phân biệt role nào được set status gì. Driver có thể gọi với `status = "Failed"` hay `status = "DriverAssigning"` — về kỹ thuật sẽ bị domain Shipment reject (vì transition không hợp lệ), nhưng đây là security smell.

**Giải pháp đề xuất:** Trong handler, validate: nếu caller là `Driver`, chỉ cho phép set `PickedUp`, `InTransit`, `Delivered`.

---

### X3 🟢 Không có health check endpoint qua Gateway

**Vấn đề:** Client không có cách check service nào đang down. `/health` và `/ready` tồn tại trên từng service nhưng không được proxy qua Gateway.

**Giải pháp đề xuất:** Expose `GET /health` aggregate qua Gateway trả về status của tất cả downstream services.

---

### X4 🟢 Rate limit GPS có thể bị throttle khi scale

**Vấn đề:**
Gateway config: `300 req/min per IP`. Driver app push GPS mỗi 1 giây = 60 req/min. Nếu 5+ driver cùng NAT (ví dụ: fleet dùng hotspot chung) → hit rate limit.

**Giải pháp đề xuất:** Áp dụng rate limit per-user (từ JWT sub) thay vì per-IP cho endpoint `/api/v1/tracking/location`. Hoặc tăng limit riêng cho endpoint này.

---

## 5. Tổng hợp endpoint còn thiếu

| Endpoint | Service | Auth | Gap được giải quyết |
|---|---|---|---|
| `GET /api/v1/orders/{id}/shipment` | Shipment | Bearer | C1 |
| `GET /api/v1/payments/orders/{orderId}/escrow` | Payment | Bearer | C2 |
| `POST /api/v1/drivers/register` (self-service) | Driver/Identity | Anonymous | C3 |
| `GET /api/v1/shipments?customerId=&status=&page=` | Shipment | Bearer | C7, A1 |
| `GET /api/v1/shipments/active?driverId=` | Shipment | Driver | D1 |
| `POST /api/v1/uploads/presigned-url` hoặc `/breakdown-photos/upload` | (mới) | Bearer | D5 |
| `GET /api/v1/payments?status=&dateFrom=&dateTo=&page=` | Payment | Admin | A2 |
| `GET /api/v1/drivers?status=&page=` | Driver | Admin | A3 |
| `GET /api/v1/vehicles?status=&driverId=&type=&page=` | Driver | Admin | A4 |
| Thêm route `/api/v1/analytics/*` vào Gateway | Gateway | Admin | A5 |
| `POST /api/v1/analytics/fraud/alerts/{id}/acknowledge` | Analytics | Admin | A7 |
| `PUT /api/v1/vehicles/{id}/status` | Driver | Admin | A8 |
| `POST /api/v1/notifications/device-tokens` | Notification | Bearer | C6, D3 |
| Pagination metadata cho tất cả list endpoints | Tất cả | — | X1 |

**Thay đổi hiện có (không thêm endpoint mới):**

| Thay đổi | File | Gap |
|---|---|---|
| Thêm `shipmentId` vào `OrderDto` | `OrderDto.cs` | C1 (phương án B) |
| Thêm `role` vào `RegisterRequest` | `AuthController.cs` | C3 (phương án A) |
| Thêm `TrustScore` vào `DriverDto` | `DriverDto.cs` | D6 |
| Thêm `[Authorize(Roles = "Admin")]` vào `PUT /drivers/{id}/status` | `DriversController.cs` | A6 |
| Order consumer cho `ShipmentCompletedEvent` | Order service (mới) | C5 |
| SignalR `DriverAssigned` event từ Tracking hub | Tracking service | D3 |
| Fix Driver role guard trong `UpdateShipmentStatus` handler | Shipment service | X2 |

---

## 6. Đề xuất ưu tiên triển khai

### Ưu tiên 1 — Blockers (client không hoạt động)

| # | Việc cần làm | Effort |
|---|---|---|
| B1 | **C1:** Embed `shipmentId` vào `OrderDto` (sync qua Kafka từ Shipment → Order) | M |
| B2 | **C5:** Thêm `ShipmentCompletedConsumer` trong Order service | S |
| B3 | **A5:** Thêm analytics route vào Gateway config | XS |
| B4 | **A1:** `GET /shipments?status=&page=` — list endpoint | M |
| B5 | **D1:** `GET /shipments/active?driverId=` | S |
| B6 | **C2:** `GET /payments/orders/{orderId}/escrow` | S |

### Ưu tiên 2 — High (UX không chấp nhận được)

| # | Việc cần làm | Effort |
|---|---|---|
| H1 | **C3:** Driver self-registration endpoint | M |
| H2 | **C4, X1:** Pagination metadata cho tất cả list endpoints | M |
| H3 | **A3:** `GET /drivers?status=&page=` | S |
| H4 | **A4:** `GET /vehicles?status=&page=` | S |
| H5 | **A2:** `GET /payments?page=` | S |
| H6 | **D3:** SignalR `DriverAssigned` event từ Tracking service | S |
| H7 | **D5:** Photo upload endpoint (hoặc pre-signed S3) | M |
| H8 | **A7:** Acknowledge fraud alert endpoint | S |
| H9 | **C6:** FCM device token registration | M |
| H10 | **A6:** Fix role guard `PUT /drivers/{id}/status` | XS |
| H11 | **X2:** Restrict Driver status values in `UpdateShipmentStatus` | XS |

### Ưu tiên 3 — Medium (chất lượng / vận hành)

| # | Việc cần làm | Effort |
|---|---|---|
| M1 | **D6:** `TrustScore` trong `DriverDto` | XS |
| M2 | **A8:** `PUT /vehicles/{id}/status` | XS |
| M3 | **D4:** Driver shipment history endpoint | S |
| M4 | **X4:** Rate limit per-user cho GPS endpoint | S |

> Effort: XS = < 1h | S = 1–3h | M = 3–8h | L = 1–2 ngày

---

## Phụ lục: Luồng nghiệp vụ hoàn chỉnh (as-is vs to-be)

### Luồng Customer — Đặt và theo dõi đơn hàng

```
AS-IS (hiện tại — bị đứt):
  1. POST /auth/register → userId ✅
  2. POST /auth/login → accessToken ✅
  3. POST /orders → orderId ✅
  4. [MẤT LUỒNG] — không biết shipmentId để track ❌
  5. GET /shipments/{shipmentId} → ??? không có shipmentId

TO-BE (sau fix):
  1. POST /auth/register → userId ✅
  2. POST /auth/login → accessToken ✅
  3. POST /orders → { orderId, shipmentId? } ✅  ← thêm shipmentId
  4. WS /hubs/tracking → JoinShipmentGroup(shipmentId) ✅
  5. GET /shipments/{shipmentId} → status, driver, ETA ✅
  6. SignalR: LocationUpdated events mỗi 1–5 giây ✅
  7. GET /payments/orders/{orderId} → payment status ✅
```

### Luồng Driver — Nhận và hoàn thành chuyến

```
AS-IS (hiện tại — thiếu):
  1. POST /auth/register → userId (role=Driver?) ❌ không set được role
  2. Admin tạo thủ công: POST /drivers ← phải có Admin
  3. POST /auth/login → accessToken ✅
  4. PUT /drivers/{id}/status {"status":"Available"} ✅
  5. [CHỜ ASSIGNMENT] — không có real-time, phải poll ❌
  6. [BREAKDOWN] — photo upload ở đâu? ❌

TO-BE (sau fix):
  1. POST /auth/register { role: "Driver" } → userId ✅
  2. POST /drivers/register { licenseNumber, phone } → driverId ✅
  3. POST /auth/login → accessToken ✅
  4. PUT /drivers/{id}/status {"status":"Available"} ✅
  5. WS /hubs/tracking → JoinDriverGroup(driverId) ✅
  6. SignalR: DriverAssigned event → show dialog ✅
  7. PUT /shipments/{id}/status {"status":"PickedUp"} ✅
  8. POST /tracking/location (mỗi 1–5 giây) ✅
  9. [BREAKDOWN] POST /uploads/breakdown-photo → url ✅
  10. POST /drivers/{id}/report-breakdown { photoUrls } ✅
  11. PUT /shipments/{id}/status {"status":"Delivered"} ✅
```

### Luồng Admin — Monitor và duyệt dispatch

```
AS-IS (hiện tại — không đủ):
  1. POST /auth/login { role:Admin } ✅
  2. GET /shipments/{id} — phải biết ID trước ❌
  3. [KHÔNG CÓ] list shipments cần duyệt ❌
  4. [KHÔNG CÓ] analytics qua Gateway ❌

TO-BE (sau fix):
  1. POST /auth/login { role:Admin } ✅
  2. GET /shipments?status=DispatcherReviewRequired ✅
  3. POST /shipments/{id}/confirm-dispatch ✅
  4. GET /drivers?status=Available ✅
  5. GET /analytics/kpis?days=30 (qua Gateway) ✅
  6. GET /analytics/fraud/alerts ✅
  7. POST /analytics/fraud/alerts/{id}/acknowledge ✅
  8. GET /payments?status=Completed&dateFrom=... ✅
```
