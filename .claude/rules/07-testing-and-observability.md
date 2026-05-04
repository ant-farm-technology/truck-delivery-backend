# Testing & Observability Rules

## Test Pyramid

```
      E2E (5%)        ← Critical flows only: Create Order → Deliver → Pay
    Integration (20%) ← DB, Kafka, Redis with Testcontainers
  Unit Tests (70%)    ← Domain logic, no infra dependencies
  Contract (5-10%)    ← Event schema, API contracts
```

## Unit Test Rules

```csharp
// Test domain behavior, not implementation
// Naming: Should_{ExpectedBehavior}_When{Condition}

public class OrderTests
{
    [Fact]
    public void Should_TransitionToAssigned_WhenDriverAssignedAndStatusIsConfirmed()
    {
        var order = Order.Create(customerId, pickup, delivery, items);
        order.UpdateStatus(OrderStatus.Confirmed);

        order.AssignDriver(Guid.NewGuid());

        order.Status.Should().Be(OrderStatus.AssignedToDriver);
    }

    [Fact]
    public void Should_ThrowDomainException_WhenTransitionIsInvalid()
    {
        var order = Order.Create(customerId, pickup, delivery, items);
        // order.Status = Pending

        var act = () => order.UpdateStatus(OrderStatus.Delivered); // invalid jump

        act.Should().Throw<DomainException>();
    }
}
```

**Unit test rules:**
- Không DB, không Kafka, không network
- Mock repositories với `IOrderRepository` interface
- Test domain methods trực tiếp (không qua handler)
- Coverage target: Domain = 90%+, Application = 70%

## Integration Test Rules

```csharp
// Dùng Testcontainers (Docker)
public class OrderIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Create_Order_Should_Persist_And_Publish_Kafka_Event()
    {
        // Arrange: real MySQL + Kafka via Testcontainers
        var command = new CreateOrderCommand(...);

        // Act
        var orderId = await _mediator.Send(command);

        // Assert DB
        var order = await _db.QueryFirstOrDefaultAsync<Order>("SELECT * FROM Orders WHERE Id = @Id", new { Id = orderId });
        order.Should().NotBeNull();

        // Assert Kafka event published
        var message = await _kafkaConsumer.ConsumeAsync("order.order.created", timeout: 5s);
        message.Should().NotBeNull();
        var @event = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Value);
        @event!.OrderId.Should().Be(orderId);
    }
}
```

## Event-driven Testing

```csharp
// Test idempotency
[Fact]
public async Task Consumer_Should_Skip_Duplicate_Event()
{
    var @event = new OrderCreatedEvent { MessageId = Guid.NewGuid(), OrderId = Guid.NewGuid() };

    // Process first time
    await _consumer.HandleAsync(@event);
    // Process second time (duplicate)
    await _consumer.HandleAsync(@event);

    // Only one shipment created
    var count = await _db.QuerySingleAsync<int>("SELECT COUNT(*) FROM Shipments WHERE OrderId = @Id", new { @event.OrderId });
    count.Should().Be(1);
}
```

## Saga Testing

```csharp
// Test compensation flow
[Fact]
public async Task Saga_Should_Compensate_When_DriverAssignment_Fails()
{
    // Arrange: order created, saga started, optimizer returns result
    // Act: simulate DriverAssigned event timeout (5 failures)
    // Assert: Order.Status = Pending (compensated)
    // Assert: Driver.Status = Available (compensated)
}
```

## Performance Testing

```
Tool: k6
Scenarios:
- Load test: 100 concurrent users, 10 min, order creation
- Tracking spike: 10k location updates/sec, 5 min
- Kafka throughput: 50k events/sec sustained
```

## Observability Rules

### Structured Logging (Serilog)

```csharp
// Mọi log phải có correlationId
_logger.LogInformation("Order {OrderId} created for Customer {CustomerId}",
    order.Id, order.CustomerId);
// KHÔNG: _logger.LogInformation("Order created");  ← no context

// Log levels:
// INFO: normal flow (order created, event published)
// WARN: abnormal but recoverable (rate limited, retry triggered)
// ERROR: failure needing attention (DLQ, saga failed)
// DEBUG: detailed trace (dev only, never production)

// KHÔNG log: password, token, card data, PII
```

### OpenTelemetry Traces

```csharp
// Mỗi service cần ActivitySource
public static class ServiceTelemetry
{
    public static readonly ActivitySource Source = new("TruckDelivery.OrderService", "1.0.0");
}

// Mỗi meaningful operation = 1 span
using var activity = ServiceTelemetry.Source.StartActivity("CreateOrder");
activity?.SetTag("order.id", order.Id.ToString());
activity?.SetTag("customer.id", order.CustomerId.ToString());

// Kafka consumer: extract trace context từ headers
// Kafka producer: inject trace context vào headers
```

### Correlation ID Propagation

```csharp
// API Gateway inject X-Correlation-Id nếu client không gửi
// Middleware propagate sang downstream:
app.Use(async (ctx, next) =>
{
    var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    ctx.Response.Headers["X-Correlation-Id"] = correlationId;
    using (LogContext.PushProperty("CorrelationId", correlationId))
        await next();
});

// Kafka events: correlationId carry qua OpenTelemetry headers (W3C traceparent)
```

### Prometheus Metrics (Required per service)

```csharp
// .NET service: phải expose /metrics endpoint
// Minimum required metrics:
private static readonly Counter OrdersCreated = Metrics.CreateCounter(
    "orders_created_total", "Total orders created");

private static readonly Histogram CommandDuration = Metrics.CreateHistogram(
    "command_handler_duration_seconds", "Command handler duration",
    new HistogramConfiguration { LabelNames = ["command"] });

// Kafka consumer metrics:
private static readonly Gauge KafkaConsumerLag = Metrics.CreateGauge(
    "kafka_consumer_lag", "Kafka consumer lag",
    new GaugeConfiguration { LabelNames = ["topic", "partition"] });
```

### Health Checks

```csharp
// Program.cs — BẮTBUỘC cho mọi service
builder.Services.AddHealthChecks()
    .AddMySql(connectionString, name: "mysql")
    .AddMongoDb(mongoConnectionString, name: "mongodb")
    .AddRedis(redisConnectionString, name: "redis")
    .AddKafka(producerConfig, name: "kafka");

app.MapHealthChecks("/health"); // liveness
app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}); // readiness
```

### Golden Signals Alerting

| Signal | Alert Condition |
|---|---|
| Error rate | > 5% per service |
| Latency (p95) | > 2s for sync APIs |
| Kafka consumer lag | > 10k messages |
| Saga failure rate | > 1% of dispatches |
| Tracking ingestion | < 1k events/sec (expected 50k) |
