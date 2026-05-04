using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Order.Domain.Entities;

public sealed class OrderItem : Entity<Guid>
{
    private OrderItem() { }

    private OrderItem(Guid id, Guid orderId, string productName, int quantity, decimal weightKg, decimal volumeCbm, decimal? lengthM, decimal? widthM, decimal? heightM, bool canTilt, string? notes)
        : base(id)
    {
        OrderId = orderId;
        ProductName = productName;
        Quantity = quantity;
        WeightKg = weightKg;
        VolumeCbm = volumeCbm;
        LengthM = lengthM;
        WidthM = widthM;
        HeightM = heightM;
        CanTilt = canTilt;
        Notes = notes;
    }

    public Guid OrderId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public int Quantity { get; private set; }
    public decimal WeightKg { get; private set; }
    public decimal VolumeCbm { get; private set; }
    public decimal? LengthM { get; private set; }
    public decimal? WidthM { get; private set; }
    public decimal? HeightM { get; private set; }
    public bool CanTilt { get; private set; }
    public string? Notes { get; private set; }

    internal static OrderItem Create(Guid orderId, string productName, int quantity, decimal weightKg, decimal volumeCbm, decimal? lengthM = null, decimal? widthM = null, decimal? heightM = null, bool canTilt = false, string? notes = null) =>
        new(Guid.NewGuid(), orderId, productName, quantity, weightKg, volumeCbm, lengthM, widthM, heightM, canTilt, notes);
}
