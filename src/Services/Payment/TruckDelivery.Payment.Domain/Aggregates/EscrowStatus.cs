namespace TruckDelivery.Payment.Domain.Aggregates;

public enum EscrowStatus
{
    Locked = 1,
    Released = 2,
    Disputed = 3,
    Refunded = 4
}
