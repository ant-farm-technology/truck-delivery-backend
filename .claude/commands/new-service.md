# /new-service — Scaffold .NET 10 Microservice

Scaffold một .NET 10 microservice mới theo DDD + Clean Architecture cho project truck delivery.

**Service name:** $ARGUMENTS

## Yêu cầu

Tạo solution và project structure sau (tên service = `$ARGUMENTS`):

```
src/Services/$ARGUMENTS/
  $ARGUMENTS.Domain/
    Aggregates/
    Entities/
    ValueObjects/
    Events/               # Domain Events
    Repositories/         # Interfaces only
    Exceptions/
    $ARGUMENTS.Domain.csproj
  $ARGUMENTS.Application/
    Commands/
    Queries/
    EventHandlers/        # Kafka event handlers (domain events → Kafka)
    DTOs/
    Interfaces/           # IUnitOfWork, IEventBus, etc.
    Behaviors/            # MediatR pipeline behaviors (logging, validation)
    $ARGUMENTS.Application.csproj
  $ARGUMENTS.Infrastructure/
    Persistence/
      EFCore/             # DbContext, Migrations, EFCore repositories (write)
      Dapper/             # Dapper query repositories (read)
      Mongo/              # MongoDriver repositories
    Messaging/
      Kafka/
        Producers/
        Consumers/
    Caching/
      Redis/
    $ARGUMENTS.Infrastructure.csproj
  $ARGUMENTS.Api/
    Controllers/
    Middlewares/
    Program.cs
    appsettings.json
    appsettings.Development.json
    Dockerfile
    $ARGUMENTS.Api.csproj
```

## Nội dung cần generate

### Program.cs phải có:
- OpenTelemetry ActivitySource registration (service name + version)
- Serilog structured logging với Loki sink
- Prometheus metrics endpoint (`/metrics`)
- Health check endpoints (`/health` liveness, `/ready` readiness)
- MediatR registration với pipeline behaviors: ValidationBehavior, LoggingBehavior
- FluentValidation registration
- Redis cache registration
- Kafka producer/consumer registration
- EFCore DbContext registration (MySQL)
- MongoDB client registration
- Correlation-Id middleware

### Domain Entity pattern:
```csharp
public sealed class ExampleAggregate : AggregateRoot
{
    private ExampleAggregate() { } // EFCore constructor

    private ExampleAggregate(Guid id, ...) { ... }

    public static ExampleAggregate Create(...) // factory method
    {
        var aggregate = new ExampleAggregate(Guid.NewGuid(), ...);
        aggregate.AddDomainEvent(new ExampleCreatedDomainEvent(aggregate.Id));
        return aggregate;
    }
}
```

### Kafka Consumer pattern:
```csharp
// Luôn có idempotency check bằng MessageId
// Luôn extract traceparent từ Kafka header cho OpenTelemetry
// Luôn có Dead Letter Queue handler
```

### Dockerfile: multi-stage build (.NET 10 SDK → runtime)

### appsettings.json phải có sections:
- ConnectionStrings (MySQL, MongoDB, Redis)
- Kafka (BootstrapServers, GroupId, Topics)
- OpenTelemetry (Endpoint)
- Loki (Endpoint)
- Prometheus (port)

## Rules
- Không tạo business logic trong Controller
- Repository interface ở Domain layer, implementation ở Infrastructure
- UnitOfWork ở Application layer
- Mọi Command/Query đi qua MediatR
