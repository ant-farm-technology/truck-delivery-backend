# /add-aggregate — Add Aggregate Root to Existing Service

Thêm một Aggregate Root mới vào một service .NET đã tồn tại theo DDD + Clean Architecture.

**Arguments:** `$ARGUMENTS` = `{ServiceName} {AggregateName}` (ví dụ: `Shipment Delivery`)

## Yêu cầu

Parse `$ARGUMENTS` thành `{ServiceName}` và `{AggregateName}`.

### 1. Domain/Aggregates/{AggregateName}.cs

```csharp
public sealed class {AggregateName} : AggregateRoot
{
    private {AggregateName}() { } // EFCore

    // Properties (private set)
    public Guid Id { get; private set; }
    public {AggregateName}Status Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Factory method (không dùng constructor trực tiếp)
    public static {AggregateName} Create(/* relevant params */)
    {
        var entity = new {AggregateName}
        {
            Id = Guid.NewGuid(),
            Status = {AggregateName}Status.Created,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        entity.AddDomainEvent(new {AggregateName}CreatedDomainEvent(entity.Id));
        return entity;
    }

    // State transition methods with guard clauses
    public void UpdateStatus({AggregateName}Status newStatus)
    {
        ValidateTransition(Status, newStatus);
        var old = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new {AggregateName}StatusChangedDomainEvent(Id, old, newStatus));
    }

    private static void ValidateTransition({AggregateName}Status from, {AggregateName}Status to)
    {
        // Define valid transitions
        var valid = from switch
        {
            {AggregateName}Status.Created => [{AggregateName}Status.Active],
            // ... define all valid transitions
            _ => throw new DomainException($"Invalid transition from {from} to {to}")
        };
        if (!valid.Contains(to))
            throw new DomainException($"Cannot transition from {from} to {to}");
    }
}
```

### 2. Domain/Aggregates/{AggregateName}Status.cs

```csharp
public enum {AggregateName}Status
{
    Created = 1,
    Active = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
```

### 3. Domain/Events/{AggregateName}CreatedDomainEvent.cs

```csharp
public sealed record {AggregateName}CreatedDomainEvent(Guid {AggregateName}Id) : IDomainEvent;
public sealed record {AggregateName}StatusChangedDomainEvent(
    Guid {AggregateName}Id,
    {AggregateName}Status OldStatus,
    {AggregateName}Status NewStatus) : IDomainEvent;
```

### 4. Domain/Exceptions/{AggregateName}NotFoundException.cs

```csharp
public sealed class {AggregateName}NotFoundException : DomainException
{
    public {AggregateName}NotFoundException(Guid id)
        : base($"{AggregateName} {id} not found") { }
}
```

### 5. Domain/Repositories/I{AggregateName}Repository.cs

```csharp
public interface I{AggregateName}Repository
{
    Task<{AggregateName}?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync({AggregateName} entity, CancellationToken ct = default);
    Task UpdateAsync({AggregateName} entity, CancellationToken ct = default);
}
```

### 6. Infrastructure/Persistence/EFCore/{AggregateName}Configuration.cs

```csharp
public sealed class {AggregateName}Configuration : IEntityTypeConfiguration<{AggregateName}>
{
    public void Configure(EntityTypeBuilder<{AggregateName}> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.ToTable("{AggregateName}s");
    }
}
```

### 7. Infrastructure/Persistence/EFCore/{AggregateName}Repository.cs

```csharp
public sealed class {AggregateName}Repository : I{AggregateName}Repository
{
    private readonly {ServiceName}DbContext _ctx;

    public async Task<{AggregateName}?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _ctx.{AggregateName}s.FindAsync([id], ct);

    public async Task AddAsync({AggregateName} entity, CancellationToken ct)
        => await _ctx.{AggregateName}s.AddAsync(entity, ct);

    public Task UpdateAsync({AggregateName} entity, CancellationToken ct)
    {
        _ctx.{AggregateName}s.Update(entity);
        return Task.CompletedTask;
    }
}
```

### 8. Application/Commands/Create{AggregateName}Command.cs + Handler

```csharp
public sealed record Create{AggregateName}Command(/* params */) : IRequest<Guid>;

public sealed class Create{AggregateName}CommandHandler : IRequestHandler<Create{AggregateName}Command, Guid>
{
    private readonly I{AggregateName}Repository _repo;
    private readonly IUnitOfWork _uow;

    public async Task<Guid> Handle(Create{AggregateName}Command cmd, CancellationToken ct)
    {
        var entity = {AggregateName}.Create(/* params */);
        await _repo.AddAsync(entity, ct);
        await _uow.CommitAsync(ct);
        return entity.Id;
    }
}
```

### 9. Application/Queries/Get{AggregateName}ByIdQuery.cs + Handler

```csharp
public sealed record Get{AggregateName}ByIdQuery(Guid Id) : IRequest<{AggregateName}Dto?>;

public sealed class Get{AggregateName}ByIdQueryHandler : IRequestHandler<Get{AggregateName}ByIdQuery, {AggregateName}Dto?>
{
    private readonly IDbConnection _db; // Dapper

    public async Task<{AggregateName}Dto?> Handle(Get{AggregateName}ByIdQuery query, CancellationToken ct)
    {
        const string sql = "SELECT * FROM {AggregateName}s WHERE Id = @Id";
        return await _db.QueryFirstOrDefaultAsync<{AggregateName}Dto>(sql, new { query.Id });
    }
}
```

### 10. Application/DTOs/{AggregateName}Dto.cs

```csharp
public sealed record {AggregateName}Dto(
    Guid Id,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

### 11. Api/Controllers/{AggregateName}sController.cs

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public sealed class {AggregateName}sController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] Create{AggregateName}Request request, CancellationToken ct)
    {
        var command = request.ToCommand();
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new ApiResponse<object>(new { id }));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new Get{AggregateName}ByIdQuery(id), ct);
        if (dto is null) return NotFound(new ApiResponse<object>(null, "{AggregateName.ToUpper()}_NOT_FOUND", "Not found"));
        return Ok(new ApiResponse<{AggregateName}Dto>(dto));
    }
}
```

### 12. EF Migration

Sau khi tạo xong:
```bash
cd src/Services/{ServiceName}/{ServiceName}.Infrastructure
dotnet ef migrations add Add{AggregateName}Table --project . --startup-project ../{ServiceName}.Api
```

## Rules
- Private constructor (EFCore), public static factory method
- Tất cả setters phải private
- Domain events phải được add trong factory method
- Repository interface ở Domain, implementation ở Infrastructure
- Query handler dùng Dapper, KHÔNG dùng EFCore
