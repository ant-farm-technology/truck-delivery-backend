using TruckDelivery.Payment.Domain.Exceptions;
using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Payment.Domain.Aggregates;

// Holds loading/unloading fee in escrow when a driver breakdown occurs.
// Released to replacement driver on delivery confirmation; refunded on dispute.
public sealed class EscrowPayment : AggregateRoot<Guid>
{
    private EscrowPayment() { }

    private EscrowPayment(Guid id) : base(id) { }

    public Guid ShipmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid OriginalDriverId { get; private set; }
    public Guid ReplacementDriverId { get; private set; }
    public decimal LockedAmount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public EscrowStatus Status { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTime LockedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public static EscrowPayment Create(
        Guid shipmentId,
        Guid orderId,
        Guid originalDriverId,
        Guid replacementDriverId,
        decimal lockedAmount,
        string currency = "VND")
    {
        if (lockedAmount <= 0)
            throw new PaymentDomainException("Escrow amount must be positive.");

        return new EscrowPayment(Guid.NewGuid())
        {
            ShipmentId = shipmentId,
            OrderId = orderId,
            OriginalDriverId = originalDriverId,
            ReplacementDriverId = replacementDriverId,
            LockedAmount = lockedAmount,
            Currency = currency,
            Status = EscrowStatus.Locked,
            LockedAt = DateTime.UtcNow
        };
    }

    public void Release(string? note = null)
    {
        EnsureLocked();
        Status = EscrowStatus.Released;
        ResolutionNote = note;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Dispute(string reason)
    {
        EnsureLocked();
        Status = EscrowStatus.Disputed;
        ResolutionNote = reason;
        ResolvedAt = DateTime.UtcNow;
    }

    public void Refund()
    {
        if (Status != EscrowStatus.Disputed)
            throw new PaymentDomainException("Only disputed escrows can be refunded.");
        Status = EscrowStatus.Refunded;
        ResolvedAt = DateTime.UtcNow;
    }

    private void EnsureLocked()
    {
        if (Status != EscrowStatus.Locked)
            throw new PaymentDomainException($"Escrow is not in Locked state (current: {Status}).");
    }
}
