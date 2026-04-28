namespace TruckDelivery.Payment.Domain.Aggregates;

public enum PaymentStatus
{
    Created = 1,
    Pending = 2,
    Authorized = 3,
    Captured = 4,
    Completed = 5,
    Failed = 6,
    Refunded = 7
}
