using TruckDelivery.Payment.Domain.Events;
using TruckDelivery.Payment.Domain.Exceptions;
using TruckDelivery.Payment.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Payment.Domain.Aggregates;

public sealed class Payment : AggregateRoot<Guid>
{
    private Payment() { }

    private Payment(Guid id, Guid orderId, Guid customerId, Guid? driverId, decimal amount, PaymentMethod method, string currency) : base(id)
    {
        OrderId = orderId;
        CustomerId = customerId;
        DriverId = driverId;
        Amount = amount;
        Method = method;
        Currency = currency;
        Status = PaymentStatus.Created;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? DriverId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Payment Create(Guid orderId, Guid customerId, decimal amount,
        PaymentMethod method = PaymentMethod.Cod, string currency = "VND", Guid? driverId = null)
    {
        if (amount <= 0) throw new PaymentDomainException("Payment amount must be positive.");
        var payment = new Payment(Guid.NewGuid(), orderId, customerId, driverId, amount, method, currency);
        payment.RaiseDomainEvent(new PaymentCreatedDomainEvent(payment.Id, orderId, customerId, amount));
        return payment;
    }

    public void MarkPending()
    {
        EnsureStatus(PaymentStatus.Created);
        Status = PaymentStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Authorize()
    {
        EnsureStatus(PaymentStatus.Pending);
        Status = PaymentStatus.Authorized;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status is not (PaymentStatus.Authorized or PaymentStatus.Captured))
            throw new PaymentDomainException($"Cannot complete payment from status '{Status}'.");
        Status = PaymentStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PaymentCompletedDomainEvent(Id, OrderId, CustomerId, Amount));
    }

    public void Fail(string reason)
    {
        FailureReason = reason;
        Status = PaymentStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PaymentFailedDomainEvent(Id, OrderId, reason));
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Completed)
            throw new PaymentDomainException("Only completed payments can be refunded.");
        Status = PaymentStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (Status != expected)
            throw new PaymentDomainException($"Expected status '{expected}' but was '{Status}'.");
    }
}
