# /new-rust-service — Scaffold Rust Microservice (Spatial/Geo)

Scaffold một Rust microservice cho spatial queries, PostGIS, và OpenStreetMap processing.

**Service name:** $ARGUMENTS

## Yêu cầu

Tạo project structure sau:

```
src/Services/$ARGUMENTS/
  Cargo.toml
  Cargo.lock
  Dockerfile
  .env.example
  src/
    main.rs
    config.rs         # cấu hình từ env vars
    errors.rs         # error types + From impls
    routes/
      mod.rs
      health.rs       # /health và /ready endpoints
      {domain}.rs     # domain-specific routes
    spatial/
      mod.rs
      postgis.rs      # PostGIS queries via sqlx
      osm.rs          # OpenStreetMap data processing
    models/
      mod.rs
      request.rs      # Deserialize từ HTTP request
      response.rs     # Serialize ra HTTP response
    telemetry.rs      # OpenTelemetry setup
```

### Cargo.toml dependencies bắt buộc:
```toml
[dependencies]
axum = { version = "0.7", features = ["macros"] }
tokio = { version = "1", features = ["full"] }
serde = { version = "1", features = ["derive"] }
serde_json = "1"
sqlx = { version = "0.8", features = ["runtime-tokio", "postgres", "uuid", "chrono"] }
uuid = { version = "1", features = ["v4", "serde"] }
chrono = { version = "0.4", features = ["serde"] }
tracing = "0.1"
tracing-subscriber = { version = "0.3", features = ["env-filter", "json"] }
opentelemetry = "0.24"
opentelemetry-otlp = { version = "0.17", features = ["grpc-tonic"] }
opentelemetry_sdk = { version = "0.24", features = ["rt-tokio"] }
tracing-opentelemetry = "0.25"
axum-tracing-opentelemetry = "0.19"
anyhow = "1"
thiserror = "1"
dotenvy = "0.15"
tower = "0.4"
tower-http = { version = "0.5", features = ["cors", "trace"] }
```

### main.rs pattern:
```rust
#[tokio::main]
async fn main() -> anyhow::Result<()> {
    // 1. Load config từ env
    // 2. Init OpenTelemetry tracer (OTLP gRPC)
    // 3. Init tracing subscriber với JSON format
    // 4. Init sqlx PgPool với PostGIS extension
    // 5. Build axum Router với routes
    // 6. Bind và serve với graceful shutdown
}
```

### PostGIS query pattern (sqlx):
```rust
// Dùng sqlx::query_as! macro với PostGIS geometry types
// Luôn dùng parameterized queries, không string interpolation
// Wrap trong instrument span
```

### Dockerfile: multi-stage
```dockerfile
# Stage 1: Builder (rust:1.82-slim)
# Stage 2: Runtime (debian:bookworm-slim)
# Non-root user
# EXPOSE 8080
```

### Health endpoints:
- `GET /health` — liveness: return 200 OK `{"status":"healthy"}`
- `GET /ready` — readiness: ping DB, return 200 hoặc 503

### OpenTelemetry:
- Service name = `$ARGUMENTS`
- Propagate `traceparent` header từ incoming requests
- Export to OTLP (configurable via env `OTEL_EXPORTER_OTLP_ENDPOINT`)

## Rules
- Dùng `tokio::spawn` không dùng `std::thread`
- Error handling: `thiserror` cho domain errors, `anyhow` cho app errors
- Không có `unwrap()` trong production code — dùng `?` operator
- Không có `unsafe` blocks trừ khi thực sự cần thiết
- Tất cả PostGIS queries phải parameterized
- Graceful shutdown: handle `SIGTERM` và `SIGINT`
