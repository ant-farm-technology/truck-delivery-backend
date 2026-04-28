namespace TruckDelivery.Shipment.Domain.Aggregates;

public enum ShipmentStatus
{
    Created = 1,
    RoutePlanning = 2,
    DriverAssigning = 3,
    DriverConfirmed = 4,
    InProgress = 5,
    Completed = 6,
    Failed = 7
}
