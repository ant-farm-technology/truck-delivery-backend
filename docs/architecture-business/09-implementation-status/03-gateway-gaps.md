# Gateway Configuration Gaps

> Cập nhật: 2026-04-30
> File cần sửa: `src/Gateway/TruckDelivery.Gateway/appsettings.json`

---

## 1. Routes hiện tại

| Route Pattern | Cluster | Status |
|---|---|---|
| `/api/v1/auth/*` | identity-cluster | ✅ |
| `/api/v1/orders/*` | order-cluster | ✅ |
| `/api/v1/drivers/*` | driver-cluster | ✅ |
| `/api/v1/shipments/*` | shipment-cluster | ✅ |
| `/api/v1/tracking/*` | tracking-cluster | ✅ |
| `/hubs/tracking/*` | tracking-cluster | ✅ |
| `/api/v1/notifications/*` | notification-cluster | ✅ |
| `/api/v1/payments/*` | payment-cluster | ✅ |
| `/api/v1/routes/*` | route-service-cluster | ✅ |
| `/api/v1/optimize/*` | optimizer-cluster | ✅ |
| `/api/v1/vehicles/*` | driver-cluster | **❌ MISSING** |
| `/api/v1/analytics/*` | analytics-cluster | **❌ MISSING** |
| `/api/v1/ocr/*` | ocr-cluster | **❌ MISSING** (service chưa tồn tại) |
| `/api/v1/uploads/*` | driver-cluster | **❌ MISSING** (pre-signed URL endpoint) |

---

## 2. Fix — Thêm 4 routes vào appsettings.json

```json
// Thêm vào "Routes":

"vehicle-route": {
  "ClusterId": "driver-cluster",
  "AuthorizationPolicy": "default",
  "Match": {
    "Path": "/api/v1/vehicles/{**catch-all}"
  }
},

"uploads-route": {
  "ClusterId": "driver-cluster",
  "AuthorizationPolicy": "default",
  "Match": {
    "Path": "/api/v1/uploads/{**catch-all}"
  }
},

"analytics-route": {
  "ClusterId": "analytics-cluster",
  "AuthorizationPolicy": "default",
  "Match": {
    "Path": "/api/v1/analytics/{**catch-all}"
  }
},

"ocr-route": {
  "ClusterId": "ocr-cluster",
  "AuthorizationPolicy": "default",
  "Match": {
    "Path": "/api/v1/ocr/{**catch-all}"
  }
}
```

```json
// Thêm vào "Clusters":

"analytics-cluster": {
  "Destinations": {
    "primary": { "Address": "http://analytics-service:8080/" }
  }
},

"ocr-cluster": {
  "Destinations": {
    "primary": { "Address": "http://ocr-service:8090/" }
  }
}
```

> `vehicle-route` và `uploads-route` tái dùng `driver-cluster` — Driver service (:8083) chạy cả driver + vehicle + uploads controllers.
>
> `analytics-cluster` cần entry mới — Analytics service (:8095) chạy độc lập.
>
> `ocr-cluster` cần entry mới — OCR Python service (:8090) chạy độc lập. **Chú ý:** service chưa tồn tại, cần tạo trước khi thêm route này.

---

## 3. Rate Limit — GPS Endpoint (Gap X4)

### Vấn đề

Rate limit hiện tại: `300 req/min per IP` cho tất cả endpoints.

Driver push GPS mỗi 1 giây = 60 req/min. Nếu 5 driver cùng NAT (hotspot chung trong kho bãi) → 300 req/min → hit rate limit → GPS bị throttle.

### Fix đề xuất

Thêm endpoint-specific rule cho `/api/v1/tracking/location` dùng `ClientIdHeader` (JWT subject, không phải IP):

```json
// appsettings.json — IpRateLimiting section:
"ClientIdHeader": "X-User-Id",

"EndpointRules": [
  {
    "Endpoint": "POST:/api/v1/tracking/location",
    "Period": "1m",
    "Limit": 120
  }
]
```

Và trong Gateway middleware: inject `X-User-Id` header từ JWT `sub` claim trước khi forward request.

> 120 req/min per user = 2 req/giây — đủ cho GPS push 1–2s interval.

---

## 4. Admin-only routes — Authorization Policy

Analytics endpoints chỉ dành cho Admin. YARP route hiện dùng `"default"` policy (any authenticated user). Cân nhắc tạo `"admin-only"` policy:

```csharp
// Program.cs của Gateway:
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("admin-only", policy =>
        policy.RequireRole("Admin"));
});
```

Sau đó sửa `analytics-route`:
```json
"analytics-route": {
  "ClusterId": "analytics-cluster",
  "AuthorizationPolicy": "admin-only",   // thay vì "default"
  "Match": { "Path": "/api/v1/analytics/{**catch-all}" }
}
```

> Analytics endpoints đều require `[Authorize(Roles = "Admin")]` ở service level, nhưng thêm gateway-level policy là defense-in-depth.

---

## 5. Health Check Aggregate (Gap X3)

Client không có cách check service nào đang down. Cân nhắc thêm:

```json
"health-route": {
  "ClusterId": "aggregated-health",
  "AuthorizationPolicy": "Anonymous",
  "Match": { "Path": "/health/{**catch-all}" }
}
```

Hoặc implement health aggregator endpoint trong Gateway `Program.cs`:

```csharp
app.MapHealthChecks("/health/all", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

Thêm downstream health checks vào Gateway:
```csharp
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://order-service:8080/health"), name: "order")
    .AddUrlGroup(new Uri("http://driver-service:8080/health"), name: "driver")
    .AddUrlGroup(new Uri("http://shipment-service:8080/health"), name: "shipment")
    // ... tất cả services
```
