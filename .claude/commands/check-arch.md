# /check-arch — Architecture Violation Scanner

Scan codebase và báo cáo các vi phạm kiến trúc trong project truck delivery.

## Các vi phạm cần kiểm tra

### 1. CQRS Violations
**EFCore trong Query handlers (vi phạm CQRS read side):**
- Tìm files trong `Application/Queries/**` có import/inject `DbContext` hoặc `IEntityRepository`
- Tìm files trong `Application/Queries/**` có `.ToListAsync()`, `.FirstOrDefaultAsync()`, `.Where(` qua EFCore

**Dapper trong Command handlers (vi phạm CQRS write side):**
- Tìm files trong `Application/Commands/**` có inject `IDbConnection` hoặc `IDbConnectionFactory`
- Tìm files trong `Application/Commands/**` có `QueryAsync`, `QueryFirstOrDefaultAsync`

### 2. Domain Layer Violations
**HTTP calls trong Domain layer:**
- Tìm `HttpClient`, `IHttpClientFactory`, `RestClient` trong `{Service}.Domain/`

**Business logic trong Controllers:**
- Tìm các Controller files có logic ngoài `_mediator.Send()`
- Tìm `if/else` blocks, business logic, database access trong Controller files

**Infrastructure leaking into Domain:**
- Tìm references đến EFCore, Dapper, Kafka, Redis trong `{Service}.Domain/`

### 3. Cross-service Communication Violations
**Direct HTTP calls giữa services trong Application/Domain layer:**
- Tìm `HttpClient` injection trong Application hoặc Domain layer (ngoài Infrastructure)
- Tìm hardcoded service URLs trong appsettings

### 4. Observability Violations
**Missing OpenTelemetry tracing:**
- Tìm các service folders không có `ActivitySource` registration trong `Program.cs`
- Tìm Kafka consumers không có trace context extraction từ headers

**Missing health endpoints:**
- Tìm các API projects không có `/health` và `/ready` route registration

### 5. Kafka Consumer Violations
**Missing idempotency check:**
- Tìm Kafka consumer files không có idempotency store check (`HasProcessedAsync`)

**Missing Dead Letter Queue:**
- Tìm Kafka consumer `ExecuteAsync` không có DLQ routing trong catch block

### 6. Repository Violations
**Non-aggregate-root exposure:**
- Tìm Repository interfaces expose child entity operations trực tiếp (không qua aggregate root)

**Transaction logic trong Repository:**
- Tìm `BeginTransaction`, `Commit`, `Rollback` trong Repository implementation files

### 7. Async Violations
**Blocking calls trong async code:**
- Tìm `.Result` hoặc `.Wait()` trong C# files
- Tìm `Thread.Sleep` trong C# files

### 8. Spatial/Routing Violations
**PostGIS logic trong .NET:**
- Tìm `NpgsqlPoint`, `PostgisGeometry`, spatial SQL trong C# files

**OR-Tools trong .NET:**
- Tìm `Google.OrTools` package references trong .NET csproj files

## Output Format

Báo cáo theo format:
```
[CRITICAL] {violation type}: {file}:{line} — {description}
[WARNING]  {violation type}: {file}:{line} — {description}
[INFO]     {suggestion}
```

Ưu tiên fix CRITICAL trước khi commit.
