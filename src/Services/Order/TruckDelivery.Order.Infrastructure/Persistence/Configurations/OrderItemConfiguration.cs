using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Order.Domain.Entities;

namespace TruckDelivery.Order.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderId).IsRequired();
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.WeightKg).HasColumnType("decimal(10,3)").IsRequired();
        builder.Property(i => i.VolumeCbm).HasColumnType("decimal(10,3)").IsRequired();
        builder.Property(i => i.Notes).HasMaxLength(500);

        builder.HasIndex(i => i.OrderId);
    }
}
