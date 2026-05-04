# /add-api-endpoint — Add Complete API Endpoint Stack

Thêm một API endpoint hoàn chỉnh (Request → Command/Query → Handler → Response) vào service đã tồn tại.

**Arguments:** `$ARGUMENTS` = `{ServiceName} {HttpMethod} {ResourcePath} {Description}`
Ví dụ: `Order POST /api/v1/orders/bulk-cancel "Cancel multiple orders"`

## Yêu cầu

Parse `$ARGUMENTS` và tạo đầy đủ:

### 1. Xác định type: Command hay Query?

- **POST, PUT, PATCH, DELETE** → **Command** (write, qua EFCore)
- **GET** → **Query** (read, qua Dapper)

### 2. Api/Requests/{Action}{Resource}Request.cs

```csharp
// API layer DTO — KHÔNG expose domain entity
public sealed record {Action}{Resource}Request(
    // ... request properties
)
{
    // Mapping method (request → command/query)
    public {Action}{Resource}Command ToCommand(Guid userId) =>
        new(userId, /* map properties */);
}
```

### 3. FluentValidation Validator

```csharp
public sealed class {Action}{Resource}RequestValidator : AbstractValidator<{Action}{Resource}Request>
{
    public {Action}{Resource}RequestValidator()
    {
        // Define validation rules
        // Validate at API boundary — không validate ở domain
    }
}
```

### 4a. Application/Commands/{Action}{Resource}Command.cs + Handler (nếu write)

```csharp
public sealed record {Action}{Resource}Command(
    Guid UserId,
    // ... command properties
) : IRequest<{Action}{Resource}Result>;

public sealed record {Action}{Resource}Result(Guid Id, /* relevant output */);

public sealed class {Action}{Resource}CommandHandler
    : IRequestHandler<{Action}{Resource}Command, {Action}{Resource}Result>
{
    private readonly I{Resource}Repository _repo;
    private readonly IUnitOfWork _uow;

    public async Task<{Action}{Resource}Result> Handle(
        {Action}{Resource}Command cmd, CancellationToken ct)
    {
        // 1. Load aggregate if needed
        // 2. Call domain method
        // 3. Save via repository
        // 4. Commit UoW (triggers outbox → domain events → Kafka)
        // 5. Return result DTO
    }
}
```

### 4b. Application/Queries/{Resource}By{Filter}Query.cs + Handler (nếu read)

```csharp
public sealed record {Resource}By{Filter}Query(
    Guid {FilterField},
    PaginationQuery Pagination
) : IRequest<PagedResult<{Resource}Dto>>;

public sealed class {Resource}By{Filter}QueryHandler
    : IRequestHandler<{Resource}By{Filter}Query, PagedResult<{Resource}Dto>>
{
    private readonly IDbConnection _db; // Dapper — KHÔNG EFCore

    public async Task<PagedResult<{Resource}Dto>> Handle(
        {Resource}By{Filter}Query query, CancellationToken ct)
    {
        const string sql = """
            SELECT Id, Status, CreatedAt
            FROM {Resource}s
            WHERE {FilterColumn} = @{FilterField}
            ORDER BY CreatedAt DESC
            LIMIT @PageSize OFFSET @Offset;

            SELECT COUNT(*) FROM {Resource}s WHERE {FilterColumn} = @{FilterField};
            """;

        using var multi = await _db.QueryMultipleAsync(sql, new
        {
            query.{FilterField},
            query.Pagination.PageSize,
            query.Pagination.Offset
        });

        var items = (await multi.ReadAsync<{Resource}Dto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<{Resource}Dto>(items, total, query.Pagination.Page, query.Pagination.PageSize);
    }
}
```

### 5. Api/Controllers/{Resource}sController.cs — Thêm action method

```csharp
// Thêm vào controller hiện có

/// <summary>{Description}</summary>
/// <remarks>
/// Requires role: {role}
/// </remarks>
[Http{Method}("{route}")]
[Authorize(Roles = "{role}")]
[ProducesResponseType(typeof(ApiResponse<{Result}>), {successCode})]
[ProducesResponseType(typeof(ApiResponse<object>), 400)]
[ProducesResponseType(typeof(ApiResponse<object>), 401)]
public async Task<IActionResult> {ActionName}(
    [FromBody] {Action}{Resource}Request request,  // hoặc [FromQuery] nếu GET
    CancellationToken ct)
{
    // Thin controller: chỉ mediator.Send()
    var command = request.ToCommand(HttpContext.GetUserId());
    var result = await _mediator.Send(command, ct);
    return {successCode == 201
        ? "CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{Result}>(result))"
        : "Ok(new ApiResponse<{Result}>(result))"};
}
```

### 6. Response DTO

```csharp
// Application/DTOs/{Resource}Dto.cs (nếu chưa có)
// hoặc inline response object
public sealed record {Action}{Resource}Response(
    Guid Id,
    // relevant response fields
    DateTime CreatedAt
);
```

### 7. DI Registration

```csharp
// Trong Program.cs hoặc ServiceCollectionExtensions.cs
// Handler tự đăng ký qua MediatR assembly scanning
// Validator cần đăng ký:
builder.Services.AddValidatorsFromAssembly(typeof({ServiceName}Application).Assembly);
```

## Checklist

- [ ] Request DTO (không expose domain entity)
- [ ] FluentValidation validator
- [ ] Command hoặc Query (không lẫn lộn)
- [ ] Handler: Command dùng EFCore/Repository, Query dùng Dapper
- [ ] Controller action: chỉ `_mediator.Send()`, không business logic
- [ ] Proper HTTP status code (201 Created, 200 OK, 202 Accepted)
- [ ] `[Authorize(Roles = "...")]` attribute
- [ ] `[ProducesResponseType]` cho Swagger
- [ ] `ApiResponse<T>` wrapper
- [ ] `X-Correlation-Id` header propagated (qua middleware)
- [ ] Input validation ở API layer, không ở domain
