using Xunit;
using FluentAssertions;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Domain.ValueObjects;
using ShipmentAggregate = TruckDelivery.Shipment.Domain.Aggregates.Shipment;

namespace TruckDelivery.Shipment.Domain.Tests.Aggregates;

public sealed class ShipmentTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();

    // â”€â”€ Create â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Create_Should_SetStatusCreated()
    {
        var shipment = CreateShipment();

        shipment.Status.Should().Be(ShipmentStatus.Created);
        shipment.OrderId.Should().Be(OrderId);
        shipment.CustomerId.Should().Be(CustomerId);
    }

    [Fact]
    public void Create_Should_RaiseShipmentCreatedEvent()
    {
        var shipment = CreateShipment();

        shipment.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "ShipmentCreatedDomainEvent");
    }

    [Fact]
    public void Create_Should_StoreCoordinates_WhenProvided()
    {
        var shipment = ShipmentAggregate.Create(
            OrderId, CustomerId, "HCM", "HCM", "HN", "HN", 100m, 1m,
            pickupLatitude: 10.762, pickupLongitude: 106.660,
            deliveryLatitude: 21.028, deliveryLongitude: 105.804);

        shipment.PickupLatitude.Should().Be(10.762);
        shipment.DeliveryLongitude.Should().Be(105.804);
    }

    // â”€â”€ TransitionTo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void TransitionTo_Should_Succeed_ForValidPath()
    {
        var shipment = CreateShipment();

        var result = shipment.TransitionTo(ShipmentStatus.RoutePlanning);

        result.IsSuccess.Should().BeTrue();
        shipment.Status.Should().Be(ShipmentStatus.RoutePlanning);
    }

    [Fact]
    public void TransitionTo_Should_Fail_ForInvalidPath()
    {
        var shipment = CreateShipment(); // Created

        var result = shipment.TransitionTo(ShipmentStatus.Completed);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Shipment.Status");
    }

    [Fact]
    public void TransitionTo_Should_RaiseStatusChangedEvent()
    {
        var shipment = CreateShipment();
        shipment.ClearDomainEvents();

        shipment.TransitionTo(ShipmentStatus.RoutePlanning);

        shipment.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "ShipmentStatusChangedDomainEvent");
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, ShipmentStatus.RoutePlanning)]
    [InlineData(ShipmentStatus.RoutePlanning, ShipmentStatus.DriverAssigning)]
    [InlineData(ShipmentStatus.DriverAssigning, ShipmentStatus.DriverConfirmed)]
    [InlineData(ShipmentStatus.DriverConfirmed, ShipmentStatus.InProgress)]
    [InlineData(ShipmentStatus.InProgress, ShipmentStatus.Completed)]
    public void TransitionTo_Should_AllowAllValidPaths(ShipmentStatus from, ShipmentStatus to)
    {
        var shipment = CreateShipmentAtStatus(from);

        var result = shipment.TransitionTo(to);

        result.IsSuccess.Should().BeTrue();
    }

    // â”€â”€ AssignDriver â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void AssignDriver_Should_Succeed_WhenDriverAssigning()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.DriverAssigning);

        var result = shipment.AssignDriver(DriverId, VehicleId);

        result.IsSuccess.Should().BeTrue();
        shipment.AssignedDriverId.Should().Be(DriverId);
        shipment.AssignedVehicleId.Should().Be(VehicleId);
        shipment.Status.Should().Be(ShipmentStatus.DriverConfirmed);
    }

    [Fact]
    public void AssignDriver_Should_Fail_WhenNotDriverAssigning()
    {
        var shipment = CreateShipment(); // Created status

        var result = shipment.AssignDriver(DriverId, VehicleId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Shipment.Assign");
    }

    // â”€â”€ FlagForDispatcherReview â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FlagForDispatcherReview_Should_SetReviewRequired()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.DriverConfirmed);

        var result = shipment.FlagForDispatcherReview("Bin check failed");

        result.IsSuccess.Should().BeTrue();
        shipment.Status.Should().Be(ShipmentStatus.DispatcherReviewRequired);
        shipment.RequiresDispatcherConfirmation.Should().BeTrue();
        shipment.BinCheckWarnings.Should().Be("Bin check failed");
    }

    // â”€â”€ ConfirmByDispatcher â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ConfirmByDispatcher_Should_TransitionToInProgress()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.DispatcherReviewRequired);

        var result = shipment.ConfirmByDispatcher();

        result.IsSuccess.Should().BeTrue();
        shipment.Status.Should().Be(ShipmentStatus.InProgress);
        shipment.RequiresDispatcherConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ConfirmByDispatcher_Should_Fail_WhenNotAwaitingReview()
    {
        var shipment = CreateShipment();

        var result = shipment.ConfirmByDispatcher();

        result.IsSuccess.Should().BeFalse();
    }

    // â”€â”€ Fail â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Fail_Should_SetStatusFailed_FromAnyStatus()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.DriverAssigning);
        var previousStatus = shipment.Status;

        var result = shipment.Fail("No driver available");

        result.IsSuccess.Should().BeTrue();
        shipment.Status.Should().Be(ShipmentStatus.Failed);
        shipment.FailureReason.Should().Be("No driver available");
    }

    [Fact]
    public void Fail_Should_RaiseStatusChangedEvent_WithCorrectPreviousStatus()
    {
        var shipment = CreateShipmentAtStatus(ShipmentStatus.DriverAssigning);
        shipment.ClearDomainEvents();

        shipment.Fail("reason");

        // C2 fix: previousStatus captured before setting Failed
        shipment.DomainEvents.Should().NotBeEmpty();
        shipment.DomainEvents[0].GetType().Name.Should().Be("ShipmentStatusChangedDomainEvent");
    }

    // â”€â”€ MarkReassigning â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void MarkReassigning_Should_CaptureOriginalDriver_WhenInProgress()
    {
        var shipment = CreateInProgressShipment();

        var result = shipment.MarkReassigning("Vehicle breakdown");

        result.IsSuccess.Should().BeTrue();
        shipment.Status.Should().Be(ShipmentStatus.Reassigning);
        shipment.OriginalBreakdownDriverId.Should().Be(DriverId);
        shipment.IsBreakdownReassignment.Should().BeTrue();
        shipment.AssignedDriverId.Should().BeNull();
        shipment.AssignedVehicleId.Should().BeNull();
    }

    [Fact]
    public void MarkReassigning_Should_Fail_WhenNotInProgress()
    {
        var shipment = CreateShipment(); // Created

        var result = shipment.MarkReassigning("breakdown");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Shipment.Reassign");
    }

    // â”€â”€ SetRoute â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void SetRoute_Should_UpdateRouteInfo()
    {
        var shipment = CreateShipment();
        var route = RouteInfo.Create(50_000, 3600).Value;

        var result = shipment.SetRoute(route);

        result.IsSuccess.Should().BeTrue();
        shipment.Route.Should().NotBeNull();
        shipment.Route!.DistanceMeters.Should().Be(50_000);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static ShipmentAggregate CreateShipment() =>
        ShipmentAggregate.Create(OrderId, CustomerId, "HCM", "HCM", "HN", "HN", 100m, 1m);

    private static ShipmentAggregate CreateShipmentAtStatus(ShipmentStatus target)
    {
        var shipment = CreateShipment();

        // Walk the state machine to reach target
        var path = new[]
        {
            ShipmentStatus.RoutePlanning,
            ShipmentStatus.DriverAssigning,
            ShipmentStatus.DriverConfirmed,
            ShipmentStatus.DispatcherReviewRequired,
            ShipmentStatus.InProgress,
            ShipmentStatus.Completed
        };

        foreach (var status in path)
        {
            if (shipment.Status == target) break;
            if (status == ShipmentStatus.DriverConfirmed)
                shipment.AssignDriver(DriverId, VehicleId);
            else if (status == ShipmentStatus.DispatcherReviewRequired)
                shipment.FlagForDispatcherReview();
            else if (status == ShipmentStatus.InProgress && shipment.Status == ShipmentStatus.DispatcherReviewRequired)
                shipment.ConfirmByDispatcher();
            else
                shipment.TransitionTo(status);

            if (shipment.Status == target) break;
        }

        return shipment;
    }

    private static ShipmentAggregate CreateInProgressShipment()
    {
        var shipment = CreateShipment();
        shipment.TransitionTo(ShipmentStatus.RoutePlanning);
        shipment.TransitionTo(ShipmentStatus.DriverAssigning);
        shipment.AssignDriver(DriverId, VehicleId);
        shipment.TransitionTo(ShipmentStatus.InProgress);
        return shipment;
    }
}
