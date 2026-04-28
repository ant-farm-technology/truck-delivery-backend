# /review-service — Full Service Compliance Review

Review toàn bộ một .NET service theo architecture rules của truck delivery system.

**Arguments:** `$ARGUMENTS` = tên service (ví dụ: `Shipment`, `Payment`, `Tracking`)

## Yêu cầu

Scan service `src/Services/$ARGUMENTS/` và báo cáo theo từng category:

---

## 1. CQRS Compliance

**Query handlers (phải dùng Dapper, không EFCore):**
```
Tìm trong: $ARGUMENTS.Application/Queries/**
- Tìm: DbContext injection, IEntityRepository injection
- Tìm: .ToListAsync(), .FirstOrDefaultAsync(), .Where( via EFCore
- Flag: [CRITICAL] nếu tìm thấy
```

**Command handlers (phải dùng EFCore Repository, không Dapper):**
```
Tìm trong: $ARGUMENTS.Application/Commands/**
- Tìm: IDbConnection injection, QueryAsync, QueryFirstOrDefaultAsync
- Flag: [CRITICAL] nếu tìm thấy
```

---

## 2. Domain Layer Purity

**Infra dependencies bị leak vào domain:**
```
Tìm trong: $ARGUMENTS.Domain/**
- Tìm: using Microsoft.EntityFrameworkCore
- Tìm: using Dapper, using Confluent.Kafka
- Tìm: HttpClient, IHttpClientFactory
- Flag: [CRITICAL] nếu tìm thấy
```

**Business logic trong Controller:**
```
Tìm trong: $ARGUMENTS.Api/Controllers/**
- Tìm: if/else blocks (ngoài null check)
- Tìm: database access, repository access
- Tìm: business rule validation
- Flag: [CRITICAL] nếu tìm thấy
```

**Aggregate pattern:**
```
Kiểm tra mỗi Aggregate trong $ARGUMENTS.Domain/Aggregates/**:
- Có private constructor? → [OK] / [WARNING]
- Có static factory method Create()?  → [OK] / [WARNING]
- Properties có private set? → [OK] / [WARNING]
- Domain events được add trong factory/methods? → [OK] / [WARNING]
```

---

## 3. Kafka Consumer Compliance

```
Tìm trong: $ARGUMENTS.Infrastructure/Messaging/Kafka/Consumers/**
```

**Idempotency check:**
- Có `HasProcessedAsync` / idempotency check trước khi process? → [OK] / [CRITICAL]
- Có `MarkProcessedAsync` sau khi process? → [OK] / [CRITICAL]

**Dead Letter Queue:**
- Có `catch` block route message to DLQ? → [OK] / [CRITICAL]
- DLQ topic name = `{original}.dlq`? → [OK] / [WARNING]

**OpenTelemetry:**
- Có extract traceparent từ Kafka headers? → [OK] / [WARNING]
- Có `ActivitySource.StartActivity` với parent context? → [OK] / [WARNING]

**Offset commit:**
- `Commit()` chỉ sau khi xử lý xong (không auto-commit)? → [OK] / [WARNING]

---

## 4. Async Code

```
Scan toàn bộ C# files trong service:
- Tìm: .Result, .Wait(), Thread.Sleep → [CRITICAL]
- Tìm: async void method (ngoài event handler) → [WARNING]
- Tìm: Task.Run cho I/O bound work → [WARNING]
```

---

## 5. Health & Observability

**Program.cs check:**
- `/health` endpoint registered? → [OK] / [CRITICAL]
- `/ready` endpoint registered? → [OK] / [CRITICAL]
- `/metrics` Prometheus endpoint? → [OK] / [WARNING]
- OpenTelemetry ActivitySource registered? → [OK] / [WARNING]
- Serilog structured logging configured? → [OK] / [WARNING]
- Correlation-Id middleware registered? → [OK] / [WARNING]

---

## 6. Repository Pattern

**Repository interfaces (Domain layer):**
- Defined in `$ARGUMENTS.Domain/Repositories/`? → [OK] / [WARNING]
- Only expose aggregate root operations? → [OK] / [WARNING]
- No `IQueryable` return types? → [OK] / [CRITICAL]

**Repository implementations (Infrastructure layer):**
- Defined in `$ARGUMENTS.Infrastructure/Persistence/EFCore/`? → [OK] / [WARNING]
- No transaction logic (BeginTransaction, Commit, Rollback)? → [OK] / [WARNING]

---

## 7. Outbox Pattern

- `OutboxMessage` table/collection exists? → [OK] / [CRITICAL]
- Domain events → OutboxMessage (not direct Kafka publish)? → [OK] / [CRITICAL]
- Background worker for outbox processing? → [OK] / [CRITICAL]

---

## 8. Spatial/Algorithm Violations

```
Scan C# files:
- Tìm: NpgsqlPoint, PostgisGeometry → [CRITICAL] (phải ở Rust service)
- Tìm: Google.OrTools namespace → [CRITICAL] (phải ở Python service)
- Tìm: NpgsqlConnection với spatial queries → [CRITICAL]
```

---

## Output Format

```
SERVICE REVIEW: $ARGUMENTS Service
====================================

CRITICAL VIOLATIONS (phải fix trước khi merge):
❌ [CQRS] QueryHandler.cs:45 — DbContext used in query handler
❌ [ASYNC] OrderService.cs:123 — .Result blocking call found

WARNINGS (nên fix):
⚠️  [OBSERVABILITY] Program.cs — /metrics endpoint missing
⚠️  [CONSUMER] OrderConsumer.cs — No OpenTelemetry trace extraction

PASSED:
✅ [DOMAIN] All aggregates have private constructors
✅ [HEALTH] /health and /ready endpoints registered
✅ [KAFKA] Idempotency check present in all consumers
✅ [DLQ] Dead Letter Queue handler present

SCORE: {passed}/{total} checks passed
RECOMMENDATION: {Fix CRITICAL issues before merging / Service is compliant}
```
