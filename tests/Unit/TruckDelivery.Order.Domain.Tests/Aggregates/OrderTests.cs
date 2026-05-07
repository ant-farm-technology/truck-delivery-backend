using Xunit;
using FluentAssertions;
using TruckDelivery.Order.Domain.Aggregates;
using TruckDelivery.Order.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Domain.Tests.Aggregates;

public sealed class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static Address ValidPickup => Address.Create("123 Nguyen Trai", "Ho Chi Minh", "Ho Chi Minh", "70000", "VN").Value;
    private static Address ValidDelivery => Address.Create("456 Le Loi", "Ha Noi", "Ha Noi", "10000", "VN").Value;

    private static IReadOnlyList<(string, int, decimal, decimal, decimal?, decimal?, decimal?, bool, string?)> OneItem =>
    [
        ("Thung hang", 1, 50m, 0.5m, 1.0m, 0.8m, 0.6m, false, null)
    ];

    // â”€â”€ Create â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Create_Should_Succeed_WithValidInputs()
    {
        var result = global::TruckDelivery.Order.Domain.Aggregates.Order.Create(
            CustomerId, ValidPickup, ValidDelivery, OneItem);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(CustomerId);
        result.Value.Status.Should().Be(OrderStatus.Pending);
        result.Value.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Create_Should_Fail_WhenItemsEmpty()
    {
        var result = global::TruckDelivery.Order.Domain.Aggregates.Order.Create(
            CustomerId, ValidPickup, ValidDelivery, []);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order.Items");
    }

    [Fact]
    public void Create_Should_ComputeTotals_FromItems()
    {
        IReadOnlyList<(string, int, decimal, decimal, decimal?, decimal?, decimal?, bool, string?)> items =
        [
            ("Box A", 2, 10m, 0.2m, null, null, null, false, null),
            ("Box B", 3, 5m, 0.1m, null, null, null, false, null)
        ];

        var order = global::TruckDelivery.Order.Domain.Aggregates.Order.Create(
            CustomerId, ValidPickup, ValidDelivery, items).Value;

        order.TotalWeightKg.Should().Be(35m);  // 2*10 + 3*5
        order.TotalVolumeCbm.Should().Be(0.7m); // 2*0.2 + 3*0.1
    }

    [Fact]
    public void Create_Should_RaiseDomainEvent()
    {
        var order = global::TruckDelivery.Order.Domain.Aggregates.Order.Create(
            CustomerId, ValidPickup, ValidDelivery, OneItem).Value;

        order.DomainEvents.Should().HaveCount(1);
        order.DomainEvents[0].GetType().Name.Should().Be("OrderCreatedDomainEvent");
    }

    [Fact]
    public void Create_Should_StoreCoordinates_WhenProvided()
    {
        var order = global::TruckDelivery.Order.Domain.Aggregates.Order.Create(
            CustomerId, ValidPickup, ValidDelivery, OneItem,
            pickupLatitude: 10.762622, pickupLongitude: 106.660172,
            deliveryLatitude: 21.028511, deliveryLongitude: 105.804817).Value;

        order.PickupLatitude.Should().Be(10.762622);
        order.PickupLongitude.Should().Be(106.660172);
        order.DeliveryLatitude.Should().Be(21.028511);
        order.DeliveryLongitude.Should().Be(105.804817);
    }

    // â”€â”€ Cancel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Cancel_Should_Succeed_WhenPending()
    {
        var order = CreatePendingOrder();

        var result = order.Cancel("Customer request");

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("Customer request");
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_Should_Fail_WhenTerminalStatus(OrderStatus terminal)
    {
        var order = CreatePendingOrder();
        order.UpdateStatus(terminal);

        var result = order.Cancel("too late");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order");
    }

    [Fact]
    public void Cancel_Should_RaiseDomainEvents()
    {
        var order = CreatePendingOrder();
        order.ClearDomainEvents();

        order.Cancel("reason");

        order.DomainEvents.Should().HaveCount(2);
        order.DomainEvents.Should().Contain(e => e.GetType().Name == "OrderCancelledDomainEvent");
        order.DomainEvents.Should().Contain(e => e.GetType().Name == "OrderStatusChangedDomainEvent");
    }

    // â”€â”€ UpdateStatus â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void UpdateStatus_Should_Succeed_WhenStatusDiffers()
    {
        var order = CreatePendingOrder();

        var result = order.UpdateStatus(OrderStatus.Confirmed);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void UpdateStatus_Should_Fail_WhenSameStatus()
    {
        var order = CreatePendingOrder();

        var result = order.UpdateStatus(OrderStatus.Pending);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Order");
    }

    [Fact]
    public void UpdateStatus_Should_RaiseStatusChangedEvent()
    {
        var order = CreatePendingOrder();
        order.ClearDomainEvents();

        order.UpdateStatus(OrderStatus.Confirmed);

        order.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "OrderStatusChangedDomainEvent");
    }

    // â”€â”€ SetShipmentId â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void SetShipmentId_Should_SetField()
    {
        var order = CreatePendingOrder();
        var shipmentId = Guid.NewGuid();

        order.SetShipmentId(shipmentId);

        order.ShipmentId.Should().Be(shipmentId);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static global::TruckDelivery.Order.Domain.Aggregates.Order CreatePendingOrder() =>
        global::TruckDelivery.Order.Domain.Aggregates.Order.Create(
            CustomerId, ValidPickup, ValidDelivery, OneItem).Value;
}
