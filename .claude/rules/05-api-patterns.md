# API Patterns — REST Conventions & Response Format

## Standard Response Format

```json
{
  "success": true,
  "data": { },
  "error": null,
  "meta": {
    "correlationId": "uuid",
    "page": 1,
    "pageSize": 20,
    "total": 100
  }
}
```

**Error response:**
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "ORDER_NOT_FOUND",
    "message": "Order not found"
  },
  "meta": { "correlationId": "uuid" }
}
```

## C# Response Wrapper

```csharp
public sealed record ApiResponse<T>(T? Data, string? ErrorCode = null, string? ErrorMessage = null)
{
    public bool Success => ErrorCode is null;
    public object? Error => ErrorCode is null ? null : new { code = ErrorCode, message = ErrorMessage };
}
```

## URL Design

```
GET    /api/v1/orders                    ← list (paginated)
GET    /api/v1/orders/{id}               ← get by id
POST   /api/v1/orders                    ← create
PUT    /api/v1/orders/{id}               ← update (idempotent)
DELETE /api/v1/orders/{id}              ← delete / cancel
GET    /api/v1/orders/{id}/tracking     ← nested resource
GET    /api/v1/orders?status=ASSIGNED&page=1&pageSize=20  ← filter + paginate
POST   /api/v1/dispatch/optimize        ← async action → 202 Accepted
```

**Verb naming:** Noun URL, không dùng `/create-order`, `/assign-driver`

## HTTP Status Codes

| Scenario | Code |
|---|---|
| Success GET/PUT | 200 |
| Created | 201 |
| Async accepted | 202 |
| No content (DELETE) | 204 |
| Validation error | 400 |
| Unauthorized (no JWT) | 401 |
| Forbidden (wrong role) | 403 |
| Not found | 404 |
| Conflict (duplicate) | 409 |
| Server error | 500 |
| Service unavailable | 503 |

## Required Headers

| Header | Direction | Purpose |
|---|---|---|
| `Authorization: Bearer <JWT>` | Request | Auth |
| `X-Correlation-Id: uuid` | Both | Distributed tracing |
| `Idempotency-Key: uuid` | Request | POST/Payment idempotency |

**API Gateway injects `X-Correlation-Id` nếu client không gửi.**

## Controller Pattern (Thin Controller)

```csharp
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create new order</summary>
    [HttpPost]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(typeof(ApiResponse<CreateOrderResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        var command = request.ToCommand(HttpContext.GetUserId());
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.OrderId },
            new ApiResponse<CreateOrderResponse>(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetOrderByIdQuery(id), ct);
        if (dto is null) return NotFound(new ApiResponse<object>(null, "ORDER_NOT_FOUND", "Order not found"));
        return Ok(new ApiResponse<OrderDto>(dto));
    }
}
```

## JWT Claims Extraction (Extension Methods)

```csharp
public static class HttpContextExtensions
{
    public static Guid GetUserId(this HttpContext ctx)
        => Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string GetUserRole(this HttpContext ctx)
        => ctx.User.FindFirstValue(ClaimTypes.Role)!;

    public static string GetCorrelationId(this HttpContext ctx)
        => ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
}
```

## Validation (API Layer)

```csharp
// Controller nhận Request DTO (not Command directly)
// FluentValidation validator cho Request DTO
// ModelState validation middleware xử lý trước khi vào controller

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.PickupAddress).NotNull();
        RuleFor(x => x.PickupAddress.City).NotEmpty();
        RuleFor(x => x.DeliveryAddress).NotNull();
        RuleFor(x => x.Items).NotEmpty();
    }
}
```

## Global Exception Middleware

```csharp
public sealed class GlobalExceptionMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
    {
        try { await next(ctx); }
        catch (DomainException ex)
        {
            ctx.Response.StatusCode = 422;
            await ctx.Response.WriteAsJsonAsync(new ApiResponse<object>(null, "DOMAIN_ERROR", ex.Message));
        }
        catch (ValidationException ex)
        {
            ctx.Response.StatusCode = 400;
            var errors = ex.Errors.Select(e => e.ErrorMessage);
            await ctx.Response.WriteAsJsonAsync(new ApiResponse<object>(null, "VALIDATION_ERROR",
                string.Join("; ", errors)));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.WriteAsJsonAsync(new ApiResponse<object>(null, "SERVER_ERROR", "Internal error"));
        }
    }
}
```

## Pagination

```csharp
// Query params: ?page=1&pageSize=20&status=ASSIGNED&sort=createdAt_desc
public sealed record PaginationQuery(int Page = 1, int PageSize = 20)
{
    public int Offset => (Page - 1) * PageSize;
}
```

## API Endpoint Reference (All Services)

| Service | Method | Path | Auth |
|---|---|---|---|
| Identity | POST | `/api/v1/auth/register` | Anonymous |
| Identity | POST | `/api/v1/auth/login` | Anonymous |
| Identity | POST | `/api/v1/auth/refresh` | Anonymous |
| Order | POST | `/api/v1/orders` | Customer |
| Order | GET | `/api/v1/orders/{id}` | Bearer |
| Order | GET | `/api/v1/orders` | Bearer |
| Order | DELETE | `/api/v1/orders/{id}` | Customer |
| Driver | POST | `/api/v1/drivers` | Admin |
| Driver | GET | `/api/v1/drivers/{id}` | Bearer |
| Driver | GET | `/api/v1/drivers/available` | Bearer |
| Driver | PUT | `/api/v1/drivers/{id}/status` | Bearer |
| Driver | POST | `/api/v1/drivers/{id}/assign-vehicle` | Admin |
| Vehicle | POST | `/api/v1/vehicles` | Admin |
| Vehicle | GET | `/api/v1/vehicles/{id}` | Bearer |
| Shipment | POST | `/api/v1/shipments/dispatch` | Admin |
| Shipment | GET | `/api/v1/shipments/{id}` | Bearer |
| Tracking | POST | `/api/v1/tracking/location` | Driver |
| Tracking | WS | `/hubs/tracking` | Bearer |
| Payment | POST | `/api/v1/payments` | System |
| Payment | POST | `/api/v1/payments/webhook` | Webhook |
| Route (Rust) | GET | `/route` | Internal |
| Route (Rust) | GET | `/matrix` | Internal |
| Route (Rust) | GET | `/nearby-drivers` | Internal |
| Optimizer | POST | `/optimize` | Internal |
