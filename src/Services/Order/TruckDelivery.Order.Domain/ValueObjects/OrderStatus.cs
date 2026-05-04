namespace TruckDelivery.Order.Domain.ValueObjects;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    AssignedToDriver = 3,
    PickedUp = 4,
    InTransit = 5,
    Delivered = 6,
    Cancelled = 7,
    Completed = 8
}
