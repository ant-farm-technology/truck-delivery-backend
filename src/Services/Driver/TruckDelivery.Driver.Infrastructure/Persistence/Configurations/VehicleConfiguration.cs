using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Driver.Domain.Aggregates;

namespace TruckDelivery.Driver.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.LicensePlate).HasMaxLength(20).IsRequired();
        builder.Property(v => v.Brand).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Model).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Type).HasConversion<int>().IsRequired();
        builder.Property(v => v.MaxWeightKg).HasColumnType("decimal(10,3)").IsRequired();
        builder.Property(v => v.MaxVolumeCbm).HasColumnType("decimal(10,3)").IsRequired();
        builder.Property(v => v.YearOfManufacture).IsRequired();
        builder.Property(v => v.Status).HasConversion<int>().IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt).IsRequired();

        builder.HasIndex(v => v.LicensePlate).IsUnique();
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.AssignedDriverId);
    }
}
