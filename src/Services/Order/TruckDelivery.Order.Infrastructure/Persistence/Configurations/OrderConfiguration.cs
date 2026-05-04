using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Order.Domain.ValueObjects;

namespace TruckDelivery.Order.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Order>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);
        builder.Property(o => o.TotalWeightKg).HasColumnType("decimal(10,3)").IsRequired();
        builder.Property(o => o.TotalVolumeCbm).HasColumnType("decimal(10,3)").IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt).IsRequired();

        builder.OwnsOne(o => o.PickupAddress, addr =>
        {
            addr.Property(a => a.Street).HasColumnName("PickupStreet").HasMaxLength(200).IsRequired();
            addr.Property(a => a.City).HasColumnName("PickupCity").HasMaxLength(100).IsRequired();
            addr.Property(a => a.Province).HasColumnName("PickupProvince").HasMaxLength(100).IsRequired();
            addr.Property(a => a.PostalCode).HasColumnName("PickupPostalCode").HasMaxLength(20);
            addr.Property(a => a.Country).HasColumnName("PickupCountry").HasMaxLength(10).IsRequired();
        });

        builder.OwnsOne(o => o.DeliveryAddress, addr =>
        {
            addr.Property(a => a.Street).HasColumnName("DeliveryStreet").HasMaxLength(200).IsRequired();
            addr.Property(a => a.City).HasColumnName("DeliveryCity").HasMaxLength(100).IsRequired();
            addr.Property(a => a.Province).HasColumnName("DeliveryProvince").HasMaxLength(100).IsRequired();
            addr.Property(a => a.PostalCode).HasColumnName("DeliveryPostalCode").HasMaxLength(20);
            addr.Property(a => a.Country).HasColumnName("DeliveryCountry").HasMaxLength(10).IsRequired();
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(o => o.PickupLatitude).HasColumnType("double");
        builder.Property(o => o.PickupLongitude).HasColumnType("double");
        builder.Property(o => o.DeliveryLatitude).HasColumnType("double");
        builder.Property(o => o.DeliveryLongitude).HasColumnType("double");

        builder.Property(o => o.ShipmentId);

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.Status);
        builder.HasIndex(o => o.CreatedAt);
        builder.HasIndex(o => o.ShipmentId);
    }
}
