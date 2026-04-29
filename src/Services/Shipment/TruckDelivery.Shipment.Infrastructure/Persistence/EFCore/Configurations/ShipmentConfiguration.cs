using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Shipment.Domain.Aggregates;
using TruckDelivery.Shipment.Domain.ValueObjects;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.EFCore.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Shipment>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Shipment> builder)
    {
        builder.ToTable("shipments");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(s => s.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").IsRequired()
            .HasConversion<int>();
        builder.Property(s => s.PickupCity).HasColumnName("pickup_city").HasMaxLength(100).IsRequired();
        builder.Property(s => s.PickupProvince).HasColumnName("pickup_province").HasMaxLength(100).IsRequired();
        builder.Property(s => s.DeliveryCity).HasColumnName("delivery_city").HasMaxLength(100).IsRequired();
        builder.Property(s => s.DeliveryProvince).HasColumnName("delivery_province").HasMaxLength(100).IsRequired();
        builder.Property(s => s.TotalWeightKg).HasColumnName("total_weight_kg").HasPrecision(10, 3).IsRequired();
        builder.Property(s => s.TotalVolumeCbm).HasColumnName("total_volume_cbm").HasPrecision(10, 3).IsRequired();
        builder.Property(s => s.AssignedDriverId).HasColumnName("assigned_driver_id");
        builder.Property(s => s.AssignedVehicleId).HasColumnName("assigned_vehicle_id");
        builder.Property(s => s.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(s => s.RequiresDispatcherConfirmation).HasColumnName("requires_dispatcher_confirmation").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.PackagesJson).HasColumnName("packages_json").HasColumnType("longtext");
        builder.Property(s => s.BinCheckWarnings).HasColumnName("bin_check_warnings").HasMaxLength(2000);
        builder.Property(s => s.OriginalBreakdownDriverId).HasColumnName("original_breakdown_driver_id");
        builder.Property(s => s.IsBreakdownReassignment).HasColumnName("is_breakdown_reassignment").HasDefaultValue(false).IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.OwnsOne(s => s.Route, r =>
        {
            r.Property(x => x.DistanceMeters).HasColumnName("route_distance_m");
            r.Property(x => x.DurationSeconds).HasColumnName("route_duration_s");
            r.Property(x => x.EncodedPolyline).HasColumnName("route_polyline").HasMaxLength(5000);
        });

        builder.HasIndex(s => s.OrderId).IsUnique();
        builder.HasIndex(s => s.Status);
        builder.Ignore(s => s.DomainEvents);
    }
}
