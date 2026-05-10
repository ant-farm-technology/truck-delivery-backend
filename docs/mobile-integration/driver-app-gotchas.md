# Driver App — Known Gotchas & Platform Issues

> Cập nhật: 2026-05-10 (thêm gotcha #11–13 sau BE-FIX-1…5)  
> Đọc file này trước khi debug bất kỳ vấn đề nào liên quan đến Driver App.

---

## 1. macOS Keychain — Error -34018

`flutter_secure_storage` trên macOS sandbox (debug build local) có thể throw:

```
PlatformException(Unexpected security result code, Code: -34018,
  Message: A required entitlement isn't present.)
```

**Nguyên nhân**: macOS sandbox yêu cầu `keychain-access-groups` entitlement, nhưng entitlement này đòi provisioning profile đầy đủ. Thêm vào `.entitlements` sẽ gây lỗi build:
```
"Runner" has entitlements that require signing with a development certificate.
```

**Giải pháp đã áp dụng**: Wrap mọi storage call trong `try/catch PlatformException`:

```dart
// Pattern chuẩn — auth_repository_impl.dart
try {
  await _storage.saveTokens(accessToken: ..., refreshToken: ...);
} on PlatformException catch (e) {
  _log.w('Keychain write failed (${e.code}): ${e.message}');
  // Continue — token in memory only for this session
}
// Áp dụng tương tự cho clearAll() trong logout()
```

Login vẫn hoạt động trong session (token trong memory). Trên device thật (iOS/Android) Keychain hoạt động bình thường.

**Không làm**:
- ❌ Thêm `keychain-access-groups` vào `.entitlements` → build fail
- ❌ Dùng `useDataProtectionKeychain: true` trong `MacOsOptions` → param không tồn tại trong v9.2.4

---

## 2. Login Response — Sau BE-FIX-1: Có `userId` và `role` trong body

Backend đã cập nhật `LoginResult` — response bây giờ trả đủ:

```json
{ "accessToken": "eyJ...", "refreshToken": "...", "expiresAt": "...", "userId": "...", "role": "Driver" }
```

Cập nhật `LoginResponse` model để parse thêm 2 fields mới. Có thể **bỏ logic `_decodeJwtPayload()`** cho `userId` và `role` sau khi model cập nhật.

**Code xử lý hiện tại** (giữ tạm, xóa sau khi cập nhật model):
```dart
// Có thể bỏ sau khi parse từ response body trực tiếp
final userId = _decodeJwtPayload(accessToken)['sub'];
final role   = _decodeJwtPayload(accessToken)['http://schemas.microsoft.com/.../role'];
```

---

## 3. build_runner bị block (Permission Denied)

Môi trường dev hiện tại không thể chạy `melos run gen` hay `build_runner`.

**Workaround**:
- `*.config.dart` (Injectable) trong `feature_profile` và `feature_driver` được maintain **thủ công**
- Khi thêm/xóa/đổi tên injectable class → cập nhật `.config.dart` tương ứng bằng tay
- **Không xóa** các file `.freezed.dart` và `.g.dart` đang tồn tại — chúng là source of truth

**Files đang được maintain thủ công**:
- `packages/features/feature_driver/lib/injection/injection.config.dart`
- `packages/features/feature_profile/lib/injection/injection.config.dart`

---

## 4. image_picker — Camera Không Hỗ Trợ trên macOS/Desktop

`ImageSource.camera` crash trên macOS với:
```
Bad state: This implementation of ImagePickerPlatform requires a "cameraDelegate"
  in order to use ImageSource.camera
```

**Giải pháp**: Luôn hiện bottom sheet chọn nguồn ảnh, check `_cameraSupported` để ẩn/hiện option camera:

```dart
// upload_documents_view.dart — _DocumentTile
bool get _cameraSupported =>
    defaultTargetPlatform == TargetPlatform.android ||
    defaultTargetPlatform == TargetPlatform.iOS;

Future<void> _pick(BuildContext context) async {
  final source = await showModalBottomSheet<ImageSource>(...);
  // Bottom sheet luôn hiện — chỉ ẩn camera option trên desktop
}
```

UX nhất quán trên mọi platform.

---

## 5. Logout Loop (sessionExpired → logout → 401 → sessionExpired)

**Nguyên nhân chuỗi**:
1. Token không được lưu vào Keychain (lỗi -34018)
2. `AuthInterceptor` không có token → mọi request đều thiếu Bearer header
3. `POST /auth/logout` trả 401 → AuthInterceptor gọi `onAuthExpired`
4. `AuthWatcherBloc._onSessionExpired()` được trigger → gọi `logout()` lại
5. Vòng lặp vô tận, crash app

**Giải pháp đã fix**:

```dart
// auth_repository_impl.dart — logout()
@override
Future<Either<AuthFailure, Unit>> logout() async {
  try {
    await _remote.logout(); // Best-effort — ignore server errors
  } catch (_) {}
  try {
    await _storage.clearAll(); // Bắt Keychain error
  } on PlatformException catch (e) {
    _log.w('Keychain clearAll failed (${e.code}): ${e.message}');
  }
  return right(unit);
}
```

---

## 6. Firebase Initialization — Phải Trước DI

Firebase **phải được init trước** `configureDependencies()` vì `DeviceRegistrationCubit` gọi `FirebaseMessaging.instance` ngay khi nhận `authenticated` event.

```dart
// apps/driver/lib/bootstrap.dart — thứ tự quan trọng
try {
  await Firebase.initializeApp(); // ← Trước tiên
} catch (e) {
  debugPrint('[Bootstrap] Firebase.initializeApp failed: $e');
}
await configureDependencies(environment); // ← Sau
runApp(DriverApp(environment: environment));
```

Dùng try/catch để app không crash nếu `GoogleService-Info.plist` chưa được cấu hình cho platform.

---

## 7. Driver App — Navigation sau khi Login

Router (`apps/driver/lib/router/app_router.dart`) gọi `GET /api/v1/drivers/{userId}` sau khi authenticated để quyết định màn hình redirect:

| Kết quả API | Route | Màn hình |
|------------|-------|----------|
| `404` (chưa có profile) | `/onboarding` | Tải ảnh lên |
| `PendingOcrVerification` / `ManualReview` | `/pending-verification` | Đang chờ duyệt |
| `OcrVerified` / `AdminVerified` / `Available` / `Busy` | `/home` | Đơn vận chuyển |
| Network error / 500 | `/home` | Fallback |

**Hàm xử lý**: `_resolveDriverDestination(AuthWatcherState authState)` trong `app_router.dart`.

---

## 8. feature_driver — DI Config Thủ Công

`feature_driver` không có code generation. Files DI maintained thủ công:

```
packages/features/feature_driver/lib/injection/
├── injection.dart           # initFeatureDriver(GetIt) function
└── injection.config.dart    # Manual DI registrations
```

**Dependency chain đã đăng ký**:
```
Dio
└── DriverOnboardingRemoteDataSourceImpl (IDriverOnboardingRemoteDataSource)
    └── DriverOnboardingRepositoryImpl (IDriverOnboardingRepository)
        └── UploadDriverDocumentsUseCase
```

**Đăng ký vào app**: `apps/driver/lib/injection/injection.dart` → `feature_driver.initFeatureDriver(getIt)`

---

## 9. DIO LogInterceptor — Chỉ Dùng Debug

`LogInterceptor` đã được thêm vào `core_network/lib/src/dio_client.dart`:

```dart
LogInterceptor(
  requestBody: true, responseBody: true, error: true,
  logPrint: (obj) => print('[DIO] $obj'),
)
```

> ⚠️ **Xóa hoặc disable trước khi build production** để tránh leak thông tin nhạy cảm trong logs.

---

## 10. Luồng Upload Ảnh (Bước 2)

Upload lên MinIO dùng presigned URL — **không dùng** `DioClient` thông thường (sẽ thêm Bearer header không cần thiết):

```dart
// Upload trực tiếp bằng Dio riêng, không có interceptor
final uploadDio = Dio();
await uploadDio.put(
  presignedUrl,
  data: File(imagePath).openRead(),
  options: Options(
    contentType: 'image/jpeg',
    headers: {'Content-Length': await File(imagePath).length()},
  ),
);
```

- URL có hiệu lực **15 phút** — lấy xong phải upload ngay
- `finalUrl` là **full URL** (có cả host MinIO public endpoint) — lưu nguyên dùng ở Bước 3
- Chỉ `uploadUrl` mới có `?X-Amz-Signature=...` — **không strip query string từ `finalUrl`**

```dart
// ĐÚNG — lưu nguyên finalUrl
final portraitFinalUrl = entry.finalUrl;
// → "http://minio:9000/driver-documents/{driverId}/portrait-{uuid}.jpg"

// SAI — logic cũ, xóa đi
// final cleanUrl = presignedUrl.split('?').first;
```

> ⚠️ MinIO `PublicEndpoint` (public) có thể khác `MinIO:Endpoint` (internal). Trong production, `finalUrl` sẽ dùng domain public — không hardcode host.

---

## 11. Enum Response — Bây giờ là String (không còn Int)

Sau BE-FIX-2, toàn bộ enum field trong response serialize thành **string name**, không còn int:

```json
// TRƯỚC (int)
{ "status": 1, "verificationStatus": 1, "licenseGrade": 3 }

// SAU (string) — đang dùng
{ "status": "Offline", "verificationStatus": "PendingOcrVerification", "licenseGrade": "C" }
```

**Model cần cập nhật:** xóa logic parse int, chỉ parse string.

> ⚠️ Nếu app đang **cache response cũ** (int) trong local storage, cần clear cache khi update lên version này.

---

## 12. `GET /drivers/me` trả `404` khi chưa submit hồ sơ — Đây là normal

`404` từ `/drivers/me` **không phải lỗi server** — nghĩa là driver chưa submit hồ sơ (chưa qua Bước 3). Redirect sang `/onboarding`, không log error.

```dart
// app_router.dart — _resolveDriverDestination()
final result = await driverRepository.getMe();
return result.fold(
  (failure) {
    if (failure is DriverNotFoundFailure) return const OnboardingRoute();
    return const HomeRoute(); // fallback cho lỗi thực sự
  },
  (driver) => _routeByVerificationStatus(driver),
);
```

**Navigation map đầy đủ:**
```
404 Not Found              → /onboarding
PendingOcrVerification
ManualReview               → /pending-verification (chờ duyệt)
OcrVerified
AdminVerified              → /home (có thể set Available)
Rejected                   → /rejected
Network error / 5xx        → /home (fallback)
```

---

## 13. `GET /vehicles/mine` — Endpoint mới thay thế 2-step query

Thay vì: `GET /drivers/me` → lấy `currentVehicleId` → `GET /vehicles/{id}`

Dùng luôn: `GET /vehicles/mine` (không cần biết vehicleId).

Gọi song song với `/drivers/me` để tối ưu:

```dart
final results = await Future.wait([
  driverRepository.getMe(),
  vehicleRepository.getMyVehicle(), // GET /api/v1/vehicles/mine
]);
```

Error `404`: driver chưa được gán xe — hiển thị UI phù hợp (placeholder xe chưa gán).
