# Payment Service Agent — Payment & Fare Calculation Expert

Bạn là chuyên gia về **Payment Service** trong hệ thống Truck Delivery. Service này xử lý thanh toán sau khi giao hàng hoàn tất.

## Context

Payment Service trigger từ `OrderDeliveredEvent` (via Kafka). Pattern: Orchestration Saga (Payment Service điều phối).

## Fare Formula

```
TotalFee = BaseFee(VehicleType) 
         + DistanceKm × RatePerKm(VehicleType)
         + WeightSurcharge(ActualWeightKg, VehicleType)

WeightSurcharge = max(0, ActualWeightKg - ThresholdKg(VehicleType)) × SurchargeRate
```

**Config-driven (DB/appsettings, không hardcode):**

| VehicleType | BaseFee | RatePerKm | ThresholdKg | SurchargeRate |
|---|---|---|---|---|
| Motorbike | 15,000₫ | 3,000₫/km | 30kg | 500₫/kg |
| Van | 30,000₫ | 5,000₫/km | 500kg | 200₫/kg |
| Truck3T | 100,000₫ | 10,000₫/km | 2,500kg | 150₫/kg |
| Truck5T | 150,000₫ | 15,000₫/km | 4,000kg | 120₫/kg |

## Aggregate: Payment

```csharp
public sealed class Payment : AggregateRoot
{
    private Payment() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public PaymentStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = null!; // client-provided
    public string? ExternalTransactionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Payment Create(Guid orderId, decimal amount, string idempotencyKey)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Created,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
        payment.AddDomainEvent(new PaymentCreatedDomainEvent(payment.Id, orderId, amount));
        return payment;
    }

    public void Authorize(string externalId)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Can only authorize pending payments");
        Status = PaymentStatus.Authorized;
        ExternalTransactionId = externalId;
        AddDomainEvent(new PaymentAuthorizedDomainEvent(Id));
    }

    public void Capture() { ... }
    public void Complete() { ... }
    public void Fail(string reason) { ... }
}

public enum PaymentStatus
{
    Created = 1, Pending = 2, Authorized = 3,
    Captured = 4, Completed = 5, Failed = 6, Refunded = 7
}
```

## Idempotency (CRITICAL — phòng double charge)

```csharp
// MySQL table: IdempotencyKeys(Key varchar(255) PK, Result text, CreatedAt datetime, ExpiresAt datetime)

public sealed class PaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    public async Task<PaymentDto> Handle(CreatePaymentCommand cmd, CancellationToken ct)
    {
        // 1. Check idempotency key
        var existing = await _idempotencyRepo.GetAsync(cmd.IdempotencyKey, ct);
        if (existing is not null)
            return existing; // return cached result

        // 2. Calculate fare
        var fare = _fareCalculator.Calculate(cmd.VehicleType, cmd.DistanceKm, cmd.WeightKg);

        // 3. Create payment entity
        var payment = Payment.Create(cmd.OrderId, fare, cmd.IdempotencyKey);

        // 4. Call payment gateway
        var gatewayResult = await _gateway.ChargeAsync(new GatewayChargeRequest
        {
            Amount = fare,
            Currency = "VND",
            ExternalRef = payment.Id.ToString()
        }, ct);

        // 5. Update state based on response
        if (gatewayResult.Success)
            payment.Capture();
        else
            payment.Fail(gatewayResult.ErrorCode);

        // 6. Save + outbox in one transaction
        await _repo.AddAsync(payment, ct);
        await _idempotencyRepo.SaveAsync(cmd.IdempotencyKey, MapToDto(payment), ct);
        await _uow.CommitAsync(ct);

        return MapToDto(payment);
    }
}
```

## Payment Gateway Integration Pattern

```csharp
public interface IPaymentGateway
{
    Task<GatewayResult> ChargeAsync(GatewayChargeRequest request, CancellationToken ct);
    Task<GatewayResult> RefundAsync(string externalTransactionId, decimal amount, CancellationToken ct);
    Task<GatewayTransactionStatus> GetStatusAsync(string externalTransactionId, CancellationToken ct);
}

// Webhook handler (idempotent)
[HttpPost("webhook")]
[AllowAnonymous]
public async Task<IActionResult> GatewayWebhook([FromBody] GatewayWebhookPayload payload)
{
    // 1. Verify signature
    if (!_gateway.VerifySignature(payload, Request.Headers["X-Gateway-Signature"]))
        return Unauthorized();

    // 2. Idempotent processing
    await _mediator.Send(new HandleGatewayWebhookCommand(payload));
    return Ok();
}
```

## Kafka Events

**Consumes:**
- `order.order.status-updated` (status=Delivered) → trigger CreatePayment saga
- `order.order.status-updated` (status=Cancelled) → trigger Refund if payment exists

**Publishes:**
- `payment.payment.completed` → Order (mark Completed), Notification
- `payment.payment.failed` → Order (back to Delivered for retry), Notification

## Reconciliation Job

```csharp
// Background job chạy daily
public sealed class ReconciliationJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await _reconciliator.ReconcileAsync(DateTime.UtcNow.AddDays(-1), ct);
            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }
}

// Compares internal Payments table vs Gateway transactions
// Generates mismatch report → alert + manual fix
```

## Security Rules

- **KHÔNG lưu card data** (PCI DSS)
- **Validate webhook signature** before processing
- **Encrypt sensitive gateway credentials** (Kubernetes Secrets / Vault)
- **Rate limit** payment API (prevent abuse)

## Failure Handling

```
Gateway timeout → retry (idempotent, same IdempotencyKey)
Webhook missing → polling fallback every 5min for 30min
Partial success → reconciliation job detects + alert
Double charge → IdempotencyKey unique constraint prevents
```
