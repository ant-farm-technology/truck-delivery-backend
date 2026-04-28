using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Order.Domain.Entities;

public sealed class OrderItem : Entity<Guid>
{
    private OrderItem() { }

    private OrderItem(Guid id, Guid orderId, string productName, int quantity, decimal weightKg, decimal volumeCbm, string? notes)
        : base(id)
    {
        OrderId = orderId;
        ProductName = productName;
        Quantity = quantity;
        WeightKg = weightKg;
        VolumeCbm = volumeCbm;
        Notes = notes;
    }

    public Guid OrderId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal WeightKg { get; private set; }
    public decimal VolumeCbm { get; private set; }
    public string? Notes { get; private set; }

    internal static OrderItem Create(Guid orderId, string productName, int quantity, decimal weightKg, decimal volumeCbm, string? notes = null) =>
        new(Guid.NewGuid(), orderId, productName, quantity, weightKg, volumeCbm, notes);
}
