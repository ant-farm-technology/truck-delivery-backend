namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public enum ShipmentSagaStatus
{
    Started = 1,
    RoutePlanned = 2,
    DriverRequested = 3,
    DriverConfirmed = 4,
    Completed = 5,
    Failed = 6
}
