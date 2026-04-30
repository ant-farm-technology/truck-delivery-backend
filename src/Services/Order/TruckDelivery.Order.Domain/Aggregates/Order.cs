using TruckDelivery.Order.Domain.Entities;
using TruckDelivery.Order.Domain.Events;
using TruckDelivery.Order.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Order.Domain.Aggregates;

public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = [];

    private Order() { }

    private Order(Guid id) : base(id) { }

    public Guid CustomerId { get; private set; }
    public Address PickupAddress { get; private set; } = default!;
    public Address DeliveryAddress { get; private set; } = default!;
    public OrderStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public decimal TotalWeightKg { get; private set; }
    public decimal TotalVolumeCbm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? ShipmentId { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public static Result<Order> Create(
        Guid customerId,
        Address pickupAddress,
        Address deliveryAddress,
        IReadOnlyList<(string ProductName, int Quantity, decimal WeightKg, decimal VolumeCbm, decimal? LengthM, decimal? WidthM, decimal? HeightM, bool CanTilt, string? Notes)> items,
        string? notes = null)
    {
        if (items.Count == 0)
            return Result.Failure<Order>(Error.Validation("Order.Items", "Order must have at least one item."));

        var order = new Order(Guid.NewGuid())
        {
            CustomerId = customerId,
            PickupAddress = pickupAddress,
            DeliveryAddress = deliveryAddress,
            Status = OrderStatus.Pending,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var (productName, quantity, weightKg, volumeCbm, lengthM, widthM, heightM, canTilt, itemNotes) in items)
        {
            var item = OrderItem.Create(order.Id, productName, quantity, weightKg, volumeCbm, lengthM, widthM, heightM, canTilt, itemNotes);
            order._items.Add(item);
            order.TotalWeightKg += weightKg * quantity;
            order.TotalVolumeCbm += volumeCbm * quantity;
        }

        order.RaiseDomainEvent(new OrderCreatedDomainEvent(order.Id, order.CustomerId, order.TotalWeightKg));
        return Result.Success(order);
    }

    public Result Cancel(string reason)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            return Result.Failure(Error.Conflict("Order", $"Cannot cancel an order with status '{Status}'."));

        var oldStatus = Status;
        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new OrderCancelledDomainEvent(Id, CustomerId, reason));
        RaiseDomainEvent(new OrderStatusChangedDomainEvent(Id, oldStatus, Status));
        return Result.Success();
    }

    public Result UpdateStatus(OrderStatus newStatus)
    {
        if (Status == newStatus)
            return Result.Failure(Error.Conflict("Order", $"Order is already in status '{Status}'."));

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new OrderStatusChangedDomainEvent(Id, oldStatus, newStatus));
        return Result.Success();
    }

    public void SetShipmentId(Guid shipmentId)
    {
        ShipmentId = shipmentId;
        UpdatedAt = DateTime.UtcNow;
    }
}
