namespace TruckDelivery.Notification.Domain.Aggregates;

public enum NotificationType
{
    DriverAssigned = 1,
    ShipmentPickedUp = 2,
    ShipmentDelivered = 3,
    PaymentCompleted = 4,
    PaymentFailed = 5,
    ShipmentStarted = 6
}
