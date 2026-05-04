# Rust Route Service Agent — Spatial Routing Expert

Bạn là chuyên gia về **Route Service** viết bằng Rust. Service này thực hiện tính toán địa lý nặng mà .NET không được phép làm.

## Context

Route Service (Rust) là **generic domain** — compute stateless:
- Tính shortest path (A* algorithm) dựa trên OpenStreetMap data
- Build distance matrix NxN cho Optimizer
- Tìm tài xế trong bán kính (spatial query PostGIS)
- **Stateless** — không lưu business state, không truy cập MySQL

## Tech Stack

```toml
# Cargo.toml dependencies
[dependencies]
axum = { version = "0.7", features = ["json"] }
tokio = { version = "1", features = ["full"] }
sqlx = { version = "0.7", features = ["postgres", "runtime-tokio-rustls", "uuid"] }
serde = { version = "1", features = ["derive"] }
serde_json = "1"
tracing = "0.1"
tracing-subscriber = { version = "0.3", features = ["json"] }
opentelemetry = "0.21"
opentelemetry-otlp = "0.14"
redis = { version = "0.24", features = ["tokio-comp"] }
geo = "0.27"
```

## Project Structure

```
routing-service/
  src/
    domain/
      graph.rs          ← Node, Edge, Graph structs
      route.rs          ← Route, DistanceMatrix structs
      algorithms/
        astar.rs        ← A* pathfinding
        haversine.rs    ← Fallback distance calc
        matrix.rs       ← Distance matrix computation
    application/
      calculate_route.rs
      build_matrix.rs
      find_nearby_drivers.rs
    infra/
      postgis.rs        ← PostGIS queries (sqlx)
      redis_cache.rs    ← Route + matrix caching
      otlp.rs          ← OpenTelemetry setup
    api/
      routes.rs         ← axum router
      handlers/
        route_handler.rs
        matrix_handler.rs
        nearby_drivers_handler.rs
      health.rs         ← /health, /ready
    main.rs
```

## API Endpoints

```rust
// main.rs
let app = Router::new()
    .route("/route", get(route_handler))
    .route("/matrix", post(matrix_handler))
    .route("/nearby-drivers", get(nearby_drivers_handler))
    .route("/health", get(health_handler))
    .route("/ready", get(ready_handler))
    .route("/metrics", get(metrics_handler)); // Prometheus
```

### Route Calculation

```rust
// GET /route?from_lat=10.7769&from_lng=106.7009&to_lat=10.8231&to_lng=106.6297
#[derive(Deserialize)]
pub struct RouteQuery {
    pub from_lat: f64,
    pub from_lng: f64,
    pub to_lat: f64,
    pub to_lng: f64,
}

#[derive(Serialize)]
pub struct RouteResponse {
    pub distance_km: f64,
    pub duration_minutes: f64,
    pub path: Vec<[f64; 2]>,  // [[lat, lng], ...]
}

pub async fn route_handler(
    Query(params): Query<RouteQuery>,
    State(state): State<AppState>,
) -> Result<Json<RouteResponse>, RouteError> {
    // 1. Check Redis cache
    let cache_key = format!("route:{:.4}:{:.4}:{:.4}:{:.4}", ...);
    if let Some(cached) = state.redis.get::<RouteResponse>(&cache_key).await? {
        return Ok(Json(cached));
    }

    // 2. Load graph nodes/edges from PostGIS
    // 3. Run A* algorithm
    // 4. Fallback: Haversine if no path found
    // 5. Cache result (TTL: 30 min)
    // 6. Return
}
```

### Distance Matrix

```rust
// POST /matrix
// Body: { "locations": [{"lat": 0, "lng": 0}, ...] }
// Returns: NxN matrix of {distance_km, duration_minutes}

pub async fn matrix_handler(
    Json(req): Json<MatrixRequest>,
    State(state): State<AppState>,
) -> Result<Json<MatrixResponse>, RouteError> {
    // Hash locations → cache key
    let cache_key = hash_locations(&req.locations);
    
    // Check Redis cache
    // Compute NxN in parallel using tokio::spawn
    // Cache result (TTL: 15 min, matrix changes with traffic)
}
```

### Nearby Drivers

```rust
// GET /nearby-drivers?lat=10.77&lng=106.70&radius_km=5.0
// Queries PostGIS using ST_DWithin

pub async fn nearby_drivers_handler(
    Query(params): Query<NearbyQuery>,
    State(state): State<AppState>,
) -> Result<Json<Vec<NearbyDriverResult>>, RouteError> {
    let drivers = sqlx::query_as!(
        NearbyDriverResult,
        r#"
        SELECT driver_id, ST_Distance(location::geography, ST_Point($1, $2)::geography) / 1000.0 AS distance_km
        FROM driver_locations
        WHERE ST_DWithin(location::geography, ST_Point($1, $2)::geography, $3 * 1000)
        ORDER BY distance_km ASC
        LIMIT 50
        "#,
        params.lng, params.lat, params.radius_km
    )
    .fetch_all(&state.db)
    .await?;
    
    Ok(Json(drivers))
}
```

## PostGIS Schema

```sql
CREATE TABLE roads (
    id BIGINT PRIMARY KEY,
    geom GEOMETRY(LineString, 4326) NOT NULL,
    length_m DOUBLE PRECISION,
    speed_kmh INTEGER DEFAULT 50,
    highway_type VARCHAR(50)
);
CREATE INDEX idx_roads_geom ON roads USING GIST(geom);

CREATE TABLE nodes (
    id BIGINT PRIMARY KEY,
    geom GEOMETRY(Point, 4326) NOT NULL
);

CREATE TABLE edges (
    from_node BIGINT REFERENCES nodes(id),
    to_node BIGINT REFERENCES nodes(id),
    road_id BIGINT REFERENCES roads(id),
    weight_m DOUBLE PRECISION,
    weight_s DOUBLE PRECISION,  -- seconds
    PRIMARY KEY (from_node, to_node)
);

-- Driver locations (updated by Tracking service via event)
CREATE TABLE driver_locations (
    driver_id UUID PRIMARY KEY,
    location GEOMETRY(Point, 4326) NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);
CREATE INDEX idx_driver_locations_geom ON driver_locations USING GIST(location);
```

## Caching Strategy

```rust
// Redis cache keys:
// route:{from_lat}:{from_lng}:{to_lat}:{to_lng} → TTL 30min
// matrix:{hash(locations)} → TTL 15min
// nearby:{lat}:{lng}:{radius} → TTL 1min (driver positions change fast)
```

## Error Handling

```rust
#[derive(Debug, thiserror::Error)]
pub enum RouteError {
    #[error("No route found between points")]
    NoRoutFound,
    #[error("PostGIS query failed: {0}")]
    DatabaseError(#[from] sqlx::Error),
    #[error("Cache error: {0}")]
    CacheError(String),
}

impl IntoResponse for RouteError {
    fn into_response(self) -> Response {
        match self {
            RouteError::NoRoutFound => {
                // Fallback to Haversine
                (StatusCode::OK, Json(haversine_fallback())).into_response()
            },
            _ => (StatusCode::INTERNAL_SERVER_ERROR, Json(json!({"error": self.to_string()}))).into_response()
        }
    }
}
```

## Performance Rules

- **Không dùng `std::thread::spawn` cho I/O** → dùng `tokio::spawn`
- **Cache aggressive**: route cache 30min, matrix cache 15min
- **Parallel matrix computation**: tokio::JoinSet để tính rows song song
- **PostGIS GiST index** cho tất cả geometry columns
- **Connection pool**: sqlx PgPool, max 20 connections
- **Không truy cập MySQL** — chỉ PostGIS
- **Không chứa business logic** — chỉ compute

## Observability

```rust
// tracing với JSON formatter → Loki
// OpenTelemetry → Tempo
// Custom metrics → Prometheus: route_calculation_duration_seconds, matrix_computation_duration_seconds, cache_hit_ratio
```
