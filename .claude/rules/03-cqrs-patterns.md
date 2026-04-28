# CQRS Patterns — Code Templates

## Command Handler Template

```csharp
// Application/Commands/CreateOrderCommandHandler.cs
public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IEventBus _eventBus;

    public CreateOrderCommandHandler(IOrderRepository repo, IUnitOfWork uow, IEventBus eventBus)
    {
        _repo = repo;
        _uow = uow;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        // 1. Create domain entity (factory method, never 'new')
        var order = Order.Create(cmd.CustomerId, cmd.PickupAddress, cmd.DeliveryAddress, cmd.Items);

        // 2. Persist via repository (EFCore write side)
        await _repo.AddAsync(order, ct);

        // 3. Save outbox entry in same transaction
        await _uow.CommitAsync(ct);

        // EventBus publishes outbox entries asynchronously
        return order.Id;
    }
}
```

## Query Handler Template

```csharp
// Application/Queries/GetOrderByIdQueryHandler.cs
public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IDbConnection _db; // Dapper — NOT EFCore

    public GetOrderByIdQueryHandler(IDbConnection db) => _db = db;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery query, CancellationToken ct)
    {
        const string sql = """
            SELECT o.Id, o.Status, o.CreatedAt, o.CustomerId,
                   oi.ProductName, oi.WeightKg
            FROM Orders o
            LEFT JOIN OrderItems oi ON oi.OrderId = o.Id
            WHERE o.Id = @Id
            """;

        // Dapper only, no EFCore tracking
        return await _db.QueryFirstOrDefaultAsync<OrderDto>(sql, new { query.Id });
    }
}
```

## Aggregate Root Pattern (BẮT BUỘC)

```csharp
public sealed class Order : AggregateRoot
{
    private Order() { } // EFCore needs parameterless constructor

    private Order(Guid id, Guid customerId, Address pickup, Address delivery)
    {
        Id = id;
        CustomerId = customerId;
        PickupAddress = pickup;
        DeliveryAddress = delivery;
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static Order Create(Guid customerId, Address pickup, Address delivery, IEnumerable<OrderItem> items)
    {
        // Guard clauses in factory, not in domain methods
        if (pickup == delivery) throw new DomainException("Pickup and delivery must differ");

        var order = new Order(Guid.NewGuid(), customerId, pickup, delivery);
        order.AddItems(items);
        order.AddDomainEvent(new OrderCreatedDomainEvent(order.Id, customerId));
        return order;
    }

    public void UpdateStatus(OrderStatus newStatus)
    {
        ValidateTransition(Status, newStatus); // guard clause
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedDomainEvent(Id, Status, newStatus));
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Address PickupAddress { get; private set; } = null!;
    public Address DeliveryAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
}
```

## Value Object Pattern

```csharp
// Immutable record — no setters
public sealed record Address(string Street, string City, string Province, string? PostalCode = null)
{
    public static Address Create(string street, string city, string province)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new DomainException("Street required");
        if (string.IsNullOrWhiteSpace(city)) throw new DomainException("City required");
        return new Address(street, city, province);
    }
}

public sealed record Location(double Latitude, double Longitude)
{
    public static Location Create(double lat, double lng)
    {
        if (lat < -90 || lat > 90) throw new DomainException("Invalid latitude");
        if (lng < -180 || lng > 180) throw new DomainException("Invalid longitude");
        return new Location(lat, lng);
    }
}
```

## Repository Interface (Domain layer)

```csharp
// Domain/Repositories/IOrderRepository.cs
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    // KHÔNG expose child entities directly
    // KHÔNG expose IQueryable
}
```

## Pipeline Behaviors (MediatR)

```
Request → ValidationBehavior → IdempotencyBehavior → TransactionBehavior → Handler → OutboxPublishBehavior
```

```csharp
// Application/Behaviors/ValidationBehavior.cs
public sealed class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

## Command Validation (FluentValidation)

```csharp
// Application/Commands/CreateOrderCommandValidator.cs
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.PickupAddress).NotNull();
        RuleFor(x => x.DeliveryAddress).NotNull();
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must have at least one item");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.WeightKg).GreaterThan(0);
            item.RuleFor(i => i.VolumeCbm).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThanOrEqualTo(1);
        });
    }
}
```

## Outbox Pattern (BẮT BUỘC cho Kafka events)

```csharp
// Không làm trực tiếp:
// await _repo.Save(order);
// await _kafkaProducer.Publish(event); // <-- SAIDUPE, không atomic!

// Làm đúng:
// 1. Save entity
// 2. Save OutboxMessage trong cùng transaction
// 3. Background job poll outbox → publish Kafka → delete

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
```

## Controller (Thin — chỉ gọi _mediator.Send())

```csharp
[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var command = new CreateOrderCommand(
            HttpContext.GetUserId(), // from JWT
            request.PickupAddress,
            request.DeliveryAddress,
            request.Items);

        var orderId = await _mediator.Send(command, ct);

        return CreatedAtAction(nameof(GetById), new { id = orderId },
            new ApiResponse<object>(new { orderId }));
    }

    // KHÔNG có business logic ở đây
    // KHÔNG có database access
    // KHÔNG có if/else business rule
}
```
