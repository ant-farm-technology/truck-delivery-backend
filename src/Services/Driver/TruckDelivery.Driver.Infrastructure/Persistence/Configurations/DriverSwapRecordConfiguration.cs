using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Driver.Domain.Aggregates;

namespace TruckDelivery.Driver.Infrastructure.Persistence.Configurations;

public sealed class DriverSwapRecordConfiguration : IEntityTypeConfiguration<DriverSwapRecord>
{
    public void Configure(EntityTypeBuilder<DriverSwapRecord> builder)
    {
        builder.ToTable("DriverSwapRecords", "driver");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.OriginalDriverId).IsRequired();
        builder.Property(r => r.ReplacementDriverId).IsRequired();
        builder.Property(r => r.ShipmentId).IsRequired();
        builder.Property(r => r.OccurredAt).IsRequired();

        builder.HasIndex(r => new { r.OriginalDriverId, r.ReplacementDriverId });
        builder.HasIndex(r => r.OccurredAt);
    }
}
