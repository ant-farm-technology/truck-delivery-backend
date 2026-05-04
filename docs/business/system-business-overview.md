# Mô Tả Nghiệp Vụ Hệ Thống Giao Hàng Xe Tải

> **Phiên bản:** 2026-04-30 | **Phạm vi:** Toàn bộ 12 microservices

---

## 1. Tổng Quan Hệ Thống

Hệ thống **Truck Delivery** là nền tảng logistics B2B/B2C kết nối **khách hàng** cần vận chuyển hàng hóa với **tài xế xe tải chuyên nghiệp**. Nền tảng điều phối toàn bộ vòng đời một đơn hàng — từ khi khách hàng đặt đơn, tìm tài xế phù hợp, theo dõi vận chuyển theo thời gian thực, đến thanh toán và phân tích hiệu quả vận hành.

### Quy mô và Mục tiêu

- Hỗ trợ **nhiều loại xe tải** (xe máy, van, xe tải 3T–15T)
- Tối ưu hóa **lộ trình + phân công tài xế** theo ràng buộc trọng tải, thể tích, thời gian giao hàng (SLA)
- Kiểm soát **gian lận** đa tầng (anti-fraud gate, trust score, mạng lưới hoán đổi tài xế)
- **Xác minh tài xế** qua OCR tài liệu (CMND, bằng lái, đăng ký xe)
- Thanh toán **COD + escrow** khi xảy ra sự cố xe hỏng

---

## 2. Các Tác Nhân (Actors)

| Tác nhân | Ứng dụng | Vai trò chính |
|---|---|---|
| **Khách hàng (Customer)** | Flutter App, NextJS Web | Tạo đơn hàng, theo dõi vận chuyển, thanh toán, xử lý escrow |
| **Tài xế (Driver)** | Flutter App | Nhận đơn, báo cáo GPS, báo hỏng xe |
| **Quản trị viên (Admin)** | Flutter App, NextJS Dashboard | Duyệt tài xế, điều phối dispatch, xem analytics, xử lý escrow |
| **Hệ thống (System)** | Backend services | Tự động hóa saga dispatch, tính phí, phát hiện gian lận |

---

## 3. Luồng Nghiệp Vụ Chính

### 3.1 Đăng ký & Xác minh Tài xế (3 bước)

```
[Bước 1] Tài xế đăng ký tài khoản
  → POST /api/v1/auth/register
  → Hệ thống tạo User (role=Driver)

[Bước 2] Tải ảnh tài liệu lên MinIO
  → GET /api/v1/uploads/presigned-url?type=driver-document
  → Nhận presigned URLs cho 7 trường ảnh
  → Tài xế upload trực tiếp lên MinIO (không qua backend)

[Bước 3] Nộp hồ sơ đăng ký đầy đủ
  → POST /api/v1/drivers/register
  → Tạo Driver + Vehicle trong 1 giao dịch
  → Trạng thái chuyển: Draft → PendingOcrVerification
  → Kafka: DriverDocumentsSubmittedEvent → topic driver.documents.submitted

[OCR Service tự động xác minh]
  → Tải 6 ảnh → OCR song song
  → Tính confidence score: CMND 40% + Bằng lái 40% + Đăng ký xe 20%
  → ≥0.85 → OcrVerified | 0.65–0.85 → ManualReview | <0.65 → Rejected
  → Kafka: DriverVerificationCompletedEvent → topic ocr.driver.verification-completed

[Admin duyệt cuối cùng (nếu ManualReview hoặc muốn kiểm tra lại)]
  → GET /api/v1/drivers/pending-verification (danh sách chờ duyệt)
  → POST /api/v1/drivers/{id}/verify → Trạng thái: AdminVerified
  → POST /api/v1/drivers/{id}/reject-verification → Trạng thái: Rejected
```

### 3.2 Luồng Đặt Hàng & Giao Hàng

```
[Khách hàng tạo đơn]
  → POST /api/v1/orders
  → Tạo Order (Pending) với địa chỉ, tọa độ GPS, danh sách hàng hóa (kích thước, trọng lượng)
  → Kafka: OrderCreatedEvent → topic order.order.created

[Shipment Service tạo Shipment]
  → Nhận OrderCreatedEvent
  → Tạo Shipment (Created) với toàn bộ thông tin gói hàng
  → DispatchSagaOrchestrator khởi động

[Saga Step 1 — Lập kế hoạch lộ trình]
  → Gọi Route Service (Rust :8084) với tọa độ pickup + delivery
  → Nhận khoảng cách (m) + thời gian (s) + encoded polyline
  → Lưu RouteInfo vào Shipment
  → Trạng thái: Created → RoutePlanning

[Saga Step 2 — Yêu cầu phân công tài xế]
  → Trạng thái: RoutePlanning → DriverAssigning
  → Kafka: DriverAssignmentRequestedEvent
  → Driver Service + Optimizer Service chọn tài xế phù hợp (OR-Tools VRP)
  → Kafka: DriverAssignedEvent → topic shipment.driver.assigned

[Shipment Service nhận kết quả phân công]
  → Gọi Optimizer /bin-check để kiểm tra gói hàng có vừa xe không
  → Nếu vừa → Trạng thái: DriverConfirmed → InProgress
  → Nếu cần review → Trạng thái: DispatcherReviewRequired
  → Kafka: ShipmentStartedEvent

[Admin xác nhận dispatch (nếu cần review)]
  → POST /api/v1/shipments/{id}/confirm-dispatch → InProgress
  → POST /api/v1/shipments/{id}/decline-dispatch → Failed

[Tài xế thực hiện giao hàng]
  → Gửi GPS liên tục qua POST /api/v1/tracking/location (mỗi 1–5s)
  → Khách hàng theo dõi real-time qua SignalR /hubs/tracking

[Giao hàng thành công]
  → Trạng thái Shipment → Completed
  → Kafka: ShipmentCompletedEvent
  → Order chuyển → Delivered
  → Payment Service tự động tạo thanh toán COD → Completed
  → Kafka: PaymentCompletedEvent
  → Order chuyển → Completed
```

### 3.3 Xử lý Xe Hỏng (Breakdown Saga)

```
[Tài xế báo hỏng xe]
  → POST /api/v1/drivers/{id}/report-breakdown
  → Anti-Fraud Gate kiểm tra:
    - Trust score ≥ 30 (mặc định 70, bị trừ khi gian lận)
    - Bắt buộc ≥ 1 ảnh chứng minh
    - GPS hiện tại ≤ 2km so với vị trí cuối cùng → Low risk
    - GPS > 2km → Medium risk
  → Nếu pass: TrustScore -= 3, Driver → Offline, Vehicle → Breakdown
  → Kafka: VehicleBreakdownEvent → topic driver.vehicle.breakdown

[Shipment Service xử lý]
  → Nhận VehicleBreakdownEvent
  → Tìm shipment InProgress của tài xế này
  → Lưu OriginalBreakdownDriverId
  → Trạng thái Shipment → Reassigning
  → BreakdownSagaOrchestrator retry mỗi 10s (tối đa 3 lần)
  → Tái kích hoạt luồng DriverAssigning → tìm tài xế mới

[Khi tìm được tài xế thay thế]
  → Kafka: BreakdownReassignmentCompletedEvent
  → Driver Service ghi lại DriverSwapRecord (cho phân tích fraud)
  → Payment Service tạo EscrowPayment (khóa 50,000 VND phí phụ thu)
  → Analytics Service ghi nhận incident + tính thời gian phục hồi

[Giải quyết Escrow]
  → POST /api/v1/payments/escrow/{id}/confirm → Released (tài xế mới giao thành công)
  → POST /api/v1/payments/escrow/{id}/dispute → Disputed (khách hàng khiếu nại)
```

### 3.4 Phát hiện Gian lận (Collusion Detection)

```
[FraudPatternAnalyzerJob chạy mỗi giờ]
  → Đọc toàn bộ DriverSwapRecord trong 30 ngày gần nhất
  → Nhóm theo cặp (originalDriverId, replacementDriverId)
  → Nếu cặp nào hoán đổi > 3 lần → nghi ngờ thông đồng
  → TrustScore cả 2 tài xế -= 10
  → Kafka: SuspiciousDriverPairDetectedEvent → topic driver.fraud.suspicious-pair-detected

[Analytics Service ghi nhận]
  → Tạo FraudAlert document trong MongoDB
  → Admin xem qua GET /api/v1/analytics/fraud/alerts
  → Admin đánh dấu đã xử lý: POST /api/v1/analytics/fraud/alerts/{id}/acknowledge
```

---

## 4. Chi Tiết Từng Service

---

### 4.1 API Gateway (Port :8080)

**Công nghệ:** .NET 10 / YARP

**Vai trò nghiệp vụ:** Điểm vào duy nhất của toàn bộ hệ thống — định tuyến request đến đúng service nội bộ.

**Chức năng chính:**
- **Rate Limiting** theo IP — bảo vệ chống DDoS và brute force
- **JWT Authentication** — xác thực token trước khi forward đến service
- **Correlation ID** — tự động gắn `X-Correlation-Id` vào mọi request (tạo mới nếu client không gửi)
- **Định tuyến:** Tất cả 12 services đều được expose qua gateway

**Không chứa business logic** — chỉ là proxy thông minh.

---

### 4.2 Identity Service (Port :8081)

**Công nghệ:** .NET 10 | MySQL (`truck_identity`)

**Vai trò nghiệp vụ:** Quản lý danh tính, xác thực và phân quyền cho toàn bộ hệ thống.

#### Nghiệp vụ chi tiết

**Đăng ký tài khoản khách hàng**
- Email (≤256 ký tự, đúng định dạng)
- Mật khẩu (8–128 ký tự)
- Họ tên (≤100 ký tự mỗi phần)
- Số điện thoại Việt Nam (định dạng `^(\+84|0)[3-9]\d{8}$`)
- Ngày sinh tùy chọn

**Đăng ký tài khoản tài xế** — cùng validation + tuổi tối thiểu 18

**Đăng nhập**
- Xác thực email + mật khẩu
- Trả về cặp (access token JWT ngắn hạn + refresh token dài hạn)
- Access token chứa: userId, email, role

**Làm mới token**
- Đổi refresh token → cặp token mới (rotation)
- Refresh token cũ bị vô hiệu hóa ngay lập tức

**Tạo tài khoản Admin** (chỉ Admin mới tạo được Admin khác)

**Seed Admin mặc định** khi hệ thống khởi động lần đầu (chạy trong `DatabaseInitializerService`)

**Sự kiện phát sinh:**
- `UserRegisteredEvent` → topic `userregistered` → Driver Service (tạo profile)

---

### 4.3 Order Service (Port :8082)

**Công nghệ:** .NET 10 | MySQL (`truck_order`)

**Vai trò nghiệp vụ:** Quản lý vòng đời đơn hàng từ khi tạo đến khi hoàn thành.

#### Trạng thái đơn hàng

```
Pending (1) → Confirmed (2) → AssignedToDriver (3) → PickedUp (4)
    → InTransit (5) → Delivered (6) → Completed (8)
    
Bất kỳ trạng thái nào (trừ Delivered, Cancelled) → Cancelled (7)
```

#### Nghiệp vụ chi tiết

**Tạo đơn hàng**
- Khách hàng cung cấp:
  - Địa chỉ lấy hàng (street, city, province, postal code, country)
  - Địa chỉ giao hàng
  - **Tọa độ GPS** pickup + delivery (tùy chọn, từ map picker — dùng để tính lộ trình thực)
  - Danh sách hàng hóa: tên, số lượng, trọng lượng (kg), thể tích (cbm), kích thước 3D (m), có nghiêng được không
- Hệ thống tự tính `TotalWeightKg` và `TotalVolumeCbm`

**Huỷ đơn hàng**
- Khách hàng huỷ với lý do (≤500 ký tự)
- Không thể huỷ khi đã `Delivered` hoặc `Cancelled`

**Cập nhật trạng thái tự động** (qua Kafka consumers):
- `OrderAssignedConsumer`: nhận `DriverAssignedEvent` → `AssignedToDriver` + gán `ShipmentId`
- `ShipmentCompletedConsumer`: nhận `ShipmentCompletedEvent` → `Delivered`
- `PaymentCompletedConsumer`: nhận `PaymentCompletedEvent` → `Completed`

**Sự kiện phát sinh:**
- `OrderCreatedEvent` → topic `order.order.created` (kèm danh sách hàng hóa + tọa độ GPS)
- `OrderCancelledEvent`

---

### 4.4 Driver/Vehicle Service (Port :8083)

**Công nghệ:** .NET 10 | MySQL (`truck_driver`) | MinIO (lưu trữ ảnh)

**Vai trò nghiệp vụ:** Quản lý hồ sơ tài xế, phương tiện, quy trình xác minh 3 bước, hệ thống trust score và phát hiện gian lận.

#### Trạng thái xác minh tài xế

```
Draft → PendingOcrVerification → OcrVerified → AdminVerified (có thể nhận đơn)
                              → ManualReview → AdminVerified / Rejected
                              → Rejected
```

**Ràng buộc:** Tài xế chỉ được chuyển sang `Available` khi đã `OcrVerified` hoặc `AdminVerified`.

#### Hạng bằng lái được chấp nhận

| Hạng | Loại xe | Ghi chú |
|---|---|---|
| B2 | Xe khách ≤30 chỗ | Được phép |
| C | Xe tải ≤10T | Được phép |
| D | Xe khách >30 chỗ | Được phép |
| FC | Xe đầu kéo + sơmi rơmoóc ≤40T | Được phép |
| FD | Xe đầu kéo + sơmi rơmoóc >40T | Được phép |
| B1 | Xe con ≤9 chỗ | **KHÔNG được phép** (vận chuyển hàng hóa) |
| E | Xe khách đặc biệt | **KHÔNG được phép** |

**Bằng lái không được hết hạn trước ngày đăng ký** (check `<` ngày hiện tại UTC).

#### Hệ thống Trust Score

- **Mặc định:** 70 điểm (khi đăng ký)
- **Phạm vi:** 0–100 (clamp)
- **Bị trừ điểm khi:**
  - Báo hỏng xe: -3 điểm
  - Phát hiện thông đồng (swap > 3 lần với cùng 1 người): -10 điểm
- **Ngưỡng:** Trust score < 30 → không được báo hỏng xe (cần điều tra trước)

#### Anti-Fraud Gate khi báo hỏng xe

Kiểm tra trước khi chấp nhận báo cáo hỏng xe:
1. Trust score tài xế ≥ 30
2. Phải đính kèm ≥ 1 ảnh chứng minh
3. GPS hiện tại so với vị trí cuối cùng (lấy từ Redis cache `driver:gps:{driverId}`):
   - ≤ 2km → **Low risk** (chấp nhận)
   - > 2km → **Medium risk** (chấp nhận nhưng flagged)
   - Không có dữ liệu GPS → **Unknown risk**
4. Nếu vượt ngưỡng → trả về HTTP 422

#### Nghiệp vụ Loại xe (VehicleType)

| Loại | Trọng tải tối đa (thông thường) |
|---|---|
| Motorbike | ≤50 kg |
| Van | ≤1T |
| Truck3T | 3T |
| Truck5T | 5T |
| Truck10T | 10T |
| Truck15T | 15T |

#### Sự kiện phát sinh:
- `DriverRegisteredEvent` → topic `driver.driver.registered`
- `DriverStatusChangedEvent` → topic `driver.driver.status-updated`
- `VehicleAssignedToDriverEvent` → topic `driver.vehicle.assigned`
- `VehicleBreakdownEvent` → topic `driver.vehicle.breakdown`
- `DriverDocumentsSubmittedEvent` → topic `driver.documents.submitted` (kích hoạt OCR)
- `SuspiciousDriverPairDetectedEvent` → topic `driver.fraud.suspicious-pair-detected`

---

### 4.5 Route Service (Port :8084)

**Công nghệ:** Rust / axum / tokio | PostGIS

**Vai trò nghiệp vụ:** Tính toán lộ trình và khoảng cách địa lý chính xác dựa trên mạng lưới đường thực tế (OpenStreetMap).

#### Nghiệp vụ chi tiết

**Tính lộ trình giữa 2 điểm**
- Input: tọa độ GPS điểm lấy hàng và điểm giao hàng
- Thuật toán: A* trên mạng lưới đường (road network) trong PostGIS
- Fallback: Haversine (đường chim bay) nếu không có đường nối
- Output: khoảng cách (m), thời gian ước tính (s), encoded polyline (hiển thị trên bản đồ)
- Cache: Redis 30 phút

**Tính ma trận khoảng cách** (cho Optimizer)
- Input: danh sách N địa điểm (lat, lng)
- Output: ma trận N×N khoảng cách và thời gian di chuyển
- Cache: Redis 15 phút

**Tìm tài xế gần nhất**
- Input: tọa độ GPS + bán kính (m) + bộ lọc (loại xe, trạng thái Available)
- Truy vấn PostGIS spatial index
- Output: danh sách tài xế sắp xếp theo khoảng cách tăng dần
- Cache: Redis 1 phút (real-time hơn)

**Cập nhật vị trí tài xế** (internal API từ Tracking Service)
- Lưu tọa độ GPS vào bảng `driver_locations` (PostGIS)

---

### 4.6 Optimizer Service (Port :8085)

**Công nghệ:** Python / FastAPI | Google OR-Tools

**Vai trò nghiệp vụ:** Giải bài toán phân công tài xế tối ưu (VRP — Vehicle Routing Problem) và kiểm tra xếp hàng vào xe (bin packing 3D).

#### Tối ưu hóa phân công (VRP)

**Mô hình bài toán:**
- Mô hình 2N+1 node: 1 depot + N điểm lấy hàng + N điểm giao hàng
- Ràng buộc **cứng:**
  - Cùng xe phải vừa lấy vừa giao một đơn (pickup + delivery trên cùng route)
  - Phải lấy hàng TRƯỚC khi giao
  - Không vượt trọng tải và thể tích xe
  - Giao hàng trong `hard_deadline_unix` (deadline cứng)
- Ràng buộc **mềm (penalty):**
  - Giao trước `desired_delivery_unix` (deadline mong muốn)
  - Lấy hàng sau `earliest_pickup_unix`
- **SLA Tier penalty scaling:**
  - Express: hệ số phạt × 3 (ưu tiên cao nhất)
  - Standard: × 1
  - Economy: × 0.5

**LIFO (Last-In-First-Out):**
- Kích hoạt bằng `enable_lifo=true`
- Hàng nào xếp vào sau phải giao trước
- Áp dụng ràng buộc O(n²) trên CumulVar (tối đa 30 đơn hàng)

**K-medoids Clustering:**
- Nhóm các đơn hàng gần nhau về địa lý vào cùng 1 xe
- Không cần tọa độ GPS — chỉ cần ma trận khoảng cách
- Phương pháp khởi tạo: farthest-first

**Timeout & Fallback:**
- Timeout OR-Tools: 5–30 giây (cấu hình được)
- Nếu timeout hoặc không feasible → Greedy fallback (tài xế gần nhất)
- Khi LIFO bật: greedy fallback vẫn tuân thủ LIFO (tất cả pickup theo thứ tự ngược, rồi delivery theo thứ tự)

#### Kiểm tra xếp hàng (Bin Check)

**Bài toán 3D Bin Packing:**
- Xe tải là "thùng" 3D (dài × rộng × cao × trọng lượng tối đa)
- Mỗi gói hàng có kích thước 3D và delivery rank (1 = giao trước)

**Các kiểm tra:**
1. **Feasibility:** tất cả gói hàng có vừa về kích thước không?
2. **Diagonal placement:** có cần xếp nghiêng không? (nếu có → cần dispatcher review)
3. **LIFO check:** hàng giao trước có ở gần cửa xe không?
4. **Priority scoring:** gói hàng có giá trị cao hơn được ưu tiên xếp vào nếu không đủ chỗ

**Kết quả:**
- Danh sách gói hàng được chấp nhận / từ chối (kèm lý do)
- Thứ tự xếp hàng (loading sequence)
- Tỷ lệ sử dụng trọng lượng và thể tích (%)
- `requires_dispatcher_confirmation` = true nếu cần xem xét thủ công

---

### 4.7 Shipment Service (Port :8086)

**Công nghệ:** .NET 10 | MySQL (`truck_shipment`) + MongoDB (saga state)

**Vai trò nghiệp vụ:** Điều phối toàn bộ quá trình giao hàng — từ khi đơn hàng được tạo đến khi hoàn thành hoặc thất bại. Đây là **Core Domain** của hệ thống.

#### Trạng thái Shipment

```
Created (1) → RoutePlanning (2) → DriverAssigning (3) → DriverConfirmed (4)
    → InProgress (5) → Completed (6)
    → DispatcherReviewRequired (8) → InProgress / Failed
    → Reassigning (9) → DriverAssigning (retry)
    → Failed (7) (từ bất kỳ trạng thái nào)
```

#### Dispatch Saga (DispatchSagaOrchestrator)

Saga tự động chạy mỗi 5 giây, xử lý shipments đang ở `Created` hoặc `RoutePlanning`:

**Step 1 — Lập kế hoạch lộ trình:**
- Nếu có tọa độ GPS → gọi Route Service thực
- Nếu Route Service không phản hồi → tính Haversine nội bộ (tốc độ trung bình 15 m/s)
- Nếu không có tọa độ → dùng placeholder 50km/3600s (backward compatibility)
- Chuyển trạng thái: `Created → RoutePlanning`

**Step 2 — Yêu cầu phân công tài xế:**
- Publish `DriverAssignmentRequestedEvent` (kèm trọng lượng, thể tích, khoảng cách)
- Chuyển trạng thái: `RoutePlanning → DriverAssigning`
- **Max retries:** 5 lần, sau đó → `Failed`

**Xử lý khi Driver được phân công (DriverAssignedConsumer):**
- Nhận `DriverAssignedEvent` từ Driver Service
- Gọi `/bin-check` Optimizer (3 lần retry, backoff 2s/4s/6s)
- Nếu bin-check pass → `DriverConfirmed → InProgress` + publish `ShipmentStartedEvent`
- Nếu bin-check cần review → `DispatcherReviewRequired` + publish `DispatcherConfirmationRequiredEvent`

#### Breakdown Saga (BreakdownSagaOrchestrator)

Khi tài xế báo hỏng xe giữa chừng:
- Lưu `OriginalBreakdownDriverId` (phục vụ phân tích fraud + escrow)
- Clear `AssignedDriverId`, `AssignedVehicleId`
- Chuyển `InProgress → Reassigning`
- Retry mỗi 10 giây, tối đa 3 lần
- Tái kích hoạt luồng DriverAssigning để tìm tài xế mới
- Sau 3 lần thất bại → `Failed`

#### Admin Controls

- **Xác nhận dispatch:** `POST /api/v1/shipments/{id}/confirm-dispatch` → `DispatcherReviewRequired → InProgress`
- **Từ chối dispatch:** `POST /api/v1/shipments/{id}/decline-dispatch` → `Failed` + `ShipmentFailedEvent`

**Sự kiện phát sinh:**
- `ShipmentCreatedEvent`
- `ShipmentStartedEvent` → topic `shipment.shipment.started`
- `ShipmentCompletedEvent` → topic `shipment.shipment.completed`
- `ShipmentFailedEvent` → topic `shipment.shipment.failed`
- `DriverAssignmentRequestedEvent` → topic `shipment.driver.assignment-requested`
- `BreakdownReassignmentCompletedEvent` → topic `shipment.breakdown.reassignment-completed`
- `DispatcherConfirmationRequiredEvent`

---

### 4.8 Tracking Service (Port :8087)

**Công nghệ:** .NET 10 + SignalR | MongoDB (`truck_tracking`) | Redis

**Vai trò nghiệp vụ:** Thu thập và phân phối dữ liệu GPS thời gian thực, cho phép khách hàng theo dõi tài xế trực tiếp.

#### Nghiệp vụ chi tiết

**Thu nhận GPS từ tài xế**
- Tài xế gửi vị trí mỗi **1–5 giây**
- Input: latitude, longitude, tốc độ (km/h, tùy chọn), hướng di chuyển (độ, tùy chọn)
- Lưu vào MongoDB (TrackingPoint)
- Cache `driver:gps:{driverId}` vào Redis TTL 10 phút → Anti-fraud gate đọc dữ liệu này

**Phân phối real-time qua SignalR**
- Khách hàng/Admin kết nối qua WebSocket: `wss://hostname/hubs/tracking`
- Auth: JWT token qua query string (`?access_token=...`)
- Groups: `tracking:{shipmentId}` (khách hàng theo dõi đơn hàng), `tracking:{driverId}` (theo dõi tài xế)
- Mỗi khi tài xế gửi GPS → broadcast đến tất cả subscriber của nhóm

**Lịch sử lộ trình**
- `GET /api/v1/tracking/shipments/{shipmentId}/points?limit=100`
- Trả về danh sách TrackingPoint theo thứ tự thời gian

**Lifecycle:**
- `ShipmentStartedConsumer` nhận → `StartTrackingCommand` → tạo TrackingSession
- `ShipmentCompletedConsumer` nhận → `StopTrackingCommand` → kết thúc session

---

### 4.9 Notification Service (Port :8088)

**Công nghệ:** .NET 10 | MySQL (`truck_notification`)

**Vai trò nghiệp vụ:** Gửi thông báo đến khách hàng và tài xế qua 3 kênh (Push/SMS/Email) khi có sự kiện trong hệ thống.

#### Nghiệp vụ chi tiết

**Đăng ký thiết bị nhận push notification**
- `POST /api/v1/notifications/register-device`
- Lưu device token (FCM/APNS) theo userId + platform (iOS/Android)
- Upsert: mỗi userId + platform chỉ có 1 token (token mới ghi đè token cũ)

**Xử lý sự kiện và gửi thông báo**

| Sự kiện nhận | Người nhận | Nội dung thông báo |
|---|---|---|
| `ShipmentStatusUpdatedEvent` | Khách hàng | "Tài xế đã được phân công", "Hàng đã được lấy", "Đang trên đường giao", "Giao hàng thành công" |
| `DriverAssignedEvent` | Tài xế | "Bạn có đơn hàng mới cần giao" |
| `PaymentCompletedEvent` | Khách hàng | "Thanh toán thành công" |

**Kênh gửi (theo mức độ ưu tiên):**
- **Khẩn cấp:** SMS + Push
- **Thời gian thực:** Push
- **Thông báo thông thường:** Email

> **Lưu ý:** Các sender hiện tại là stub (chỉ ghi log). Cần tích hợp FCM/Twilio/SMTP trong production.

---

### 4.10 Payment Service (Port :8089)

**Công nghệ:** .NET 10 | MySQL (`truck_payment`)

**Vai trò nghiệp vụ:** Xử lý thanh toán COD (Cash on Delivery) và quản lý escrow khi xảy ra sự cố.

#### Thanh toán COD (luồng chính)

```
Khi OrderDelivered → CreatePaymentCommand
  → Tạo Payment (Created)
  → Tự động Complete (COD — thanh toán khi giao)
  → Publish PaymentCompletedEvent
  → Order chuyển → Completed
```

**Công thức tính phí:**
```
TotalFee = BaseFee(VehicleType)
         + DistanceKm × RatePerKm(VehicleType)
         + max(0, ActualWeightKg - ThresholdKg) × SurchargeRate
```
Các hệ số (BaseFee, RatePerKm, ThresholdKg, SurchargeRate) lấy từ cấu hình — không hardcode.

#### Trạng thái Payment

```
Created → Pending → Authorized → Captured → Completed → Refunded (optional)
                                                     ↓
                                               Failed (terminal)
```

#### Escrow Payment (luồng xe hỏng)

**Mục đích:** Giữ phí phụ thu khi tài xế bị hoán đổi do xe hỏng (50,000 VND), giải quyết sau khi giao hàng.

**Kích hoạt:** `BreakdownReassignmentCompletedEvent` → `CreateEscrowCommand`

**Trạng thái Escrow:**
```
Locked → Released (khách hàng/admin xác nhận giao thành công)
       → Disputed (khách hàng khiếu nại)
            → Refunded (admin hoàn tiền sau điều tra)
```

**API giải quyết:**
- `POST /api/v1/payments/escrow/{id}/confirm` → Released
- `POST /api/v1/payments/escrow/{id}/dispute` → Disputed → cần admin xử lý thủ công

**Sự kiện phát sinh:**
- `PaymentCompletedEvent` → topic `payment.payment.completed`
- `PaymentFailedEvent` → topic `payment.payment.failed`

---

### 4.11 Analytics Service (Port :8095)

**Công nghệ:** .NET 10 | MongoDB (`truck_analytics`)

**Vai trò nghiệp vụ:** Thu thập dữ liệu vận hành từ các service khác, tổng hợp KPI và phát cảnh báo gian lận cho Admin.

#### KPI Dashboard

`GET /api/v1/analytics/kpis?days=30` trả về:

| Chỉ số | Ý nghĩa |
|---|---|
| Tổng số sự cố hỏng xe | Số lần báo breakdown trong N ngày qua |
| Tỷ lệ tái phân công thành công | % shipments bị breakdown được giao thành công với tài xế khác |
| Thời gian phục hồi trung bình (phút) | Từ lúc báo hỏng đến khi tài xế mới được phân công |
| Số cảnh báo gian lận | Số FraudAlert chưa được acknowledge |

#### Incidents Dashboard

`GET /api/v1/analytics/breakdown/incidents?days=30&limit=50`
- Danh sách chi tiết từng sự cố: tài xế, xe, shipment, thời gian, mức độ rủi ro, kết quả tái phân công

#### Fraud Alerts Dashboard

`GET /api/v1/analytics/fraud/alerts?days=30&limit=50`
- Danh sách cặp tài xế bị nghi ngờ thông đồng
- Admin đánh dấu đã xem xét: `POST /api/v1/analytics/fraud/alerts/{id}/acknowledge`

#### Prometheus Metrics (cho Grafana)

Phát mỗi 1 phút qua `MetricsPublisherJob`:
- `analytics_reassignment_success_rate_pct` (gauge)
- `analytics_avg_recovery_time_minutes` (gauge)
- `analytics_breakdown_incidents_total{risk_level}` (counter)
- `analytics_fraud_alerts_total` (counter)

**Kafka consumers:**
- `VehicleBreakdownConsumer`: ghi nhận `BreakdownIncident`
- `BreakdownReassignmentCompletedConsumer`: cập nhật kết quả + tính `RecoveryTimeMinutes`
- `SuspiciousDriverPairConsumer`: tạo `FraudAlert`

---

### 4.12 OCR Service (Port :8090)

**Công nghệ:** Python / FastAPI | PaddleOCR (tiếng Việt)

**Vai trò nghiệp vụ:** Tự động xác minh tính hợp lệ của tài liệu tài xế (CMND, bằng lái, đăng ký xe) qua OCR, giảm tải công việc thủ công cho Admin.

#### Tài liệu xác minh

| Tài liệu | Ảnh cần | Thông tin trích xuất |
|---|---|---|
| CMND/CCCD | Mặt trước + mặt sau | Họ tên, ngày sinh, số CMND |
| Bằng lái xe | Mặt trước + mặt sau | Hạng bằng, ngày hết hạn, số bằng |
| Đăng ký xe | Mặt trước + mặt sau | Biển số, chủ sở hữu, ngày hết hạn đăng ký |

#### Quy trình xác minh tự động

```
[Nhận DriverDocumentsSubmittedEvent]
  → Tải 6 ảnh song song từ MinIO
  → Chạy OCR đồng thời trên 3 loại tài liệu
  → 6 cross-checks:
    - Tên trên CMND = Tên trên bằng lái?
    - Ngày sinh trên CMND = Ngày sinh trên bằng lái?
    - Số CMND = owner_id trên đăng ký xe?
    - Hạng bằng có hợp lệ không (B2/C/D/FC/FD)?
    - Bằng lái chưa hết hạn?
    - Đăng ký xe chưa hết hạn?
  → Tính tổng điểm: CMND 40% + Bằng lái 40% + Đăng ký xe 20%
  → Quyết định:
    - ≥ 0.85 → ocr_verified (tự động approve)
    - 0.65–0.85 → manual_review (chuyển Admin duyệt)
    - < 0.65 → rejected (từ chối, tài xế cần nộp lại)
  → Publish DriverVerificationCompletedEvent
```

**Idempotency:** Mỗi `message_id` chỉ xử lý 1 lần (Redis TTL 24h) — tránh OCR trùng lặp khi Kafka retry.

**API trực tiếp (cho Admin/Testing):**
- `POST /api/v1/ocr/extract/id-card` — OCR CMND từ ảnh URL
- `POST /api/v1/ocr/extract/license` — OCR bằng lái
- `POST /api/v1/ocr/extract/vehicle-reg` — OCR đăng ký xe

---

## 5. Nghiệp Vụ Đặc Biệt

### 5.1 Phân loại Rủi ro Hỏng xe (FraudRiskLevel)

| Mức | Điều kiện | Hành động |
|---|---|---|
| Unknown | Không có dữ liệu GPS trước đó | Chấp nhận, flag để review |
| Low | GPS di chuyển ≤ 2km | Chấp nhận tự động |
| Medium | GPS di chuyển > 2km | Chấp nhận nhưng flagged |
| High | Trust score < 30 | Từ chối (HTTP 422) |
| Confirmed | Phát hiện qua FraudPatternAnalyzerJob | FraudAlert tạo ra |

### 5.2 Collusion Detection (Phát hiện Thông đồng)

Hai tài xế A và B bị nghi ngờ thông đồng khi:
- A báo hỏng xe khi đang giao hàng
- B được phân công thay thế
- Chuỗi này lặp lại > 3 lần trong cửa sổ 30 ngày

**Hậu quả:** Cả A và B đều bị trừ 10 điểm trust score + FraudAlert gửi Admin.

### 5.3 Tích hợp Hệ thống Quan sát (Observability)

| Công cụ | Dữ liệu | Endpoint |
|---|---|---|
| Prometheus | Metrics từ tất cả services | `/metrics` |
| Grafana Loki | Structured logs (Serilog JSON) | — |
| Grafana Tempo | Distributed traces (OpenTelemetry) | — |
| Grafana Dashboard | Visualize metrics + logs + traces | :3000 |

**Correlation ID** được propagate qua toàn bộ hệ thống (header `X-Correlation-Id` + OpenTelemetry `traceparent`).

---

## 6. Luồng Dữ Liệu Kafka (Tóm tắt)

| Topic | Producer | Consumer(s) | Mục đích |
|---|---|---|---|
| `userregistered` | Identity | Driver | Tạo Driver profile khi User đăng ký |
| `order.order.created` | Order | Shipment | Khởi động dispatch saga |
| `driver.documents.submitted` | Driver | OCR | Kích hoạt xác minh tài liệu tự động |
| `ocr.driver.verification-completed` | OCR | Driver | Cập nhật kết quả xác minh |
| `shipment.driver.assignment-requested` | Shipment | Driver | Yêu cầu tìm tài xế |
| `shipment.driver.assigned` | Shipment | Driver, Order, Tracking | Tài xế đã được phân công |
| `shipment.shipment.started` | Shipment | Tracking, Notification | Bắt đầu tracking session |
| `shipment.shipment.completed` | Shipment | Order, Payment, Tracking | Giao hàng thành công |
| `shipment.shipment.failed` | Shipment | Order | Giao hàng thất bại |
| `driver.vehicle.breakdown` | Driver | Shipment, Analytics | Báo hỏng xe |
| `shipment.breakdown.reassignment-completed` | Shipment | Driver, Payment, Analytics | Tái phân công hoàn thành |
| `driver.fraud.suspicious-pair-detected` | Driver | Analytics | Phát hiện cặp tài xế nghi vấn |
| `payment.payment.completed` | Payment | Order, Notification | Thanh toán hoàn tất |
| `payment.payment.failed` | Payment | Order, Notification | Thanh toán thất bại |

---

## 7. Tổng Hợp Điểm Kiểm Soát Nghiệp Vụ

| Điểm kiểm soát | Tầng | Cơ chế |
|---|---|---|
| Xác thực đầu vào người dùng | API (FluentValidation) | Validator trên Command/Request DTO |
| Phân quyền endpoint | API (JWT) | `[Authorize(Roles = "...")]` |
| Tính hợp lệ của transition trạng thái | Domain (Guard clauses) | `IsValidTransition()` trong aggregate |
| Chống gian lận hỏng xe | Application (Gate) | `IBreakdownFraudGate` |
| Chống duplicate event | Infrastructure (Redis) | `RedisIdempotencyStore` (TTL 24h) |
| Chống duplicate message Kafka | Infrastructure | At-least-once + idempotency check |
| Chống duplicate API request | API | `Idempotency-Key` header (Payment) |
| Tính toán tối ưu | External (Python) | OR-Tools VRP + bin packing |
| Xác minh tài liệu | External (OCR) | PaddleOCR + 6 cross-checks |
| Kiểm tra GPS | External (Rust) | PostGIS spatial queries |
