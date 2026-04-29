using TruckDelivery.Shipment.Domain.Events;
using TruckDelivery.Shipment.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Shipment.Domain.Aggregates;

public sealed class Shipment : AggregateRoot<Guid>
{
    private Shipment() { }

    private Shipment(Guid id) : base(id) { }

    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string PickupCity { get; private set; } = null!;
    public string PickupProvince { get; private set; } = null!;
    public string DeliveryCity { get; private set; } = null!;
    public string DeliveryProvince { get; private set; } = null!;
    public decimal TotalWeightKg { get; private set; }
    public decimal TotalVolumeCbm { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public Guid? AssignedVehicleId { get; private set; }
    public RouteInfo? Route { get; private set; }
    public string? FailureReason { get; private set; }
    public bool RequiresDispatcherConfirmation { get; private set; }
    public string? PackagesJson { get; private set; }
    public string? BinCheckWarnings { get; private set; }
    // Breakdown tracking fields
    public Guid? OriginalBreakdownDriverId { get; private set; }
    public bool IsBreakdownReassignment { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Shipment Create(
        Guid orderId,
        Guid customerId,
        string pickupCity,
        string pickupProvince,
        string deliveryCity,
        string deliveryProvince,
        decimal totalWeightKg,
        decimal totalVolumeCbm)
    {
        var shipment = new Shipment(Guid.NewGuid())
        {
            OrderId = orderId,
            CustomerId = customerId,
            Status = ShipmentStatus.Created,
            PickupCity = pickupCity,
            PickupProvince = pickupProvince,
            DeliveryCity = deliveryCity,
            DeliveryProvince = deliveryProvince,
            TotalWeightKg = totalWeightKg,
            TotalVolumeCbm = totalVolumeCbm,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        shipment.RaiseDomainEvent(new ShipmentCreatedDomainEvent(shipment.Id, orderId, customerId));
        return shipment;
    }

    public Result TransitionTo(ShipmentStatus newStatus)
    {
        if (!IsValidTransition(Status, newStatus))
            return Result.Failure(Error.Conflict("Shipment.Status",
                $"Cannot transition from '{Status}' to '{newStatus}'."));

        var old = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ShipmentStatusChangedDomainEvent(Id, OrderId, old, newStatus));
        return Result.Success();
    }

    public Result AssignDriver(Guid driverId, Guid vehicleId)
    {
        if (Status != ShipmentStatus.DriverAssigning)
            return Result.Failure(Error.Conflict("Shipment.Assign",
                "Driver can only be assigned when status is DriverAssigning."));

        AssignedDriverId = driverId;
        AssignedVehicleId = vehicleId;
        UpdatedAt = DateTime.UtcNow;
        return TransitionTo(ShipmentStatus.DriverConfirmed);
    }

    public Result SetRoute(RouteInfo route)
    {
        Route = route;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void StorePackages(string packagesJson)
    {
        PackagesJson = packagesJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result FlagForDispatcherReview(string? binCheckWarnings = null)
    {
        RequiresDispatcherConfirmation = true;
        BinCheckWarnings = binCheckWarnings;
        UpdatedAt = DateTime.UtcNow;
        return TransitionTo(ShipmentStatus.DispatcherReviewRequired);
    }

    public Result ConfirmByDispatcher()
    {
        if (Status != ShipmentStatus.DispatcherReviewRequired)
            return Result.Failure(Error.Conflict("Shipment.Confirm", "Shipment is not awaiting dispatcher review."));

        RequiresDispatcherConfirmation = false;
        UpdatedAt = DateTime.UtcNow;
        return TransitionTo(ShipmentStatus.InProgress);
    }

    public Result Fail(string reason)
    {
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
        Status = ShipmentStatus.Failed;
        RaiseDomainEvent(new ShipmentStatusChangedDomainEvent(Id, OrderId, Status, ShipmentStatus.Failed));
        return Result.Success();
    }

    public Result MarkReassigning(string breakdownReason)
    {
        if (Status != ShipmentStatus.InProgress)
            return Result.Failure(Error.Conflict("Shipment.Reassign",
                "Can only reassign shipments that are InProgress."));

        OriginalBreakdownDriverId = AssignedDriverId;
        IsBreakdownReassignment = true;
        AssignedDriverId = null;
        AssignedVehicleId = null;
        FailureReason = breakdownReason;
        UpdatedAt = DateTime.UtcNow;
        return TransitionTo(ShipmentStatus.Reassigning);
    }

    private static bool IsValidTransition(ShipmentStatus from, ShipmentStatus to) =>
        (from, to) switch
        {
            (ShipmentStatus.Created, ShipmentStatus.RoutePlanning) => true,
            (ShipmentStatus.RoutePlanning, ShipmentStatus.DriverAssigning) => true,
            (ShipmentStatus.DriverAssigning, ShipmentStatus.DriverConfirmed) => true,
            (ShipmentStatus.DriverConfirmed, ShipmentStatus.InProgress) => true,
            (ShipmentStatus.DriverConfirmed, ShipmentStatus.DispatcherReviewRequired) => true,
            (ShipmentStatus.DispatcherReviewRequired, ShipmentStatus.InProgress) => true,
            (ShipmentStatus.InProgress, ShipmentStatus.Completed) => true,
            (ShipmentStatus.InProgress, ShipmentStatus.Reassigning) => true,
            (ShipmentStatus.Reassigning, ShipmentStatus.DriverAssigning) => true,
            (_, ShipmentStatus.Failed) => true,
            _ => false
        };
}
