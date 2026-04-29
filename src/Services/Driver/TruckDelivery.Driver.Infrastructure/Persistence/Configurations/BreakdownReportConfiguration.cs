using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Infrastructure.Persistence.Configurations;

public sealed class BreakdownReportConfiguration : IEntityTypeConfiguration<BreakdownReport>
{
    public void Configure(EntityTypeBuilder<BreakdownReport> builder)
    {
        builder.ToTable("BreakdownReports", "driver");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DriverId).IsRequired();
        builder.Property(r => r.VehicleId);
        builder.Property(r => r.Latitude).IsRequired();
        builder.Property(r => r.Longitude).IsRequired();
        builder.Property(r => r.PhotoUrlsJson).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.FraudRiskLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(r => r.ReviewNote).HasMaxLength(500);
        builder.Property(r => r.ReportedAt).IsRequired();

        builder.HasIndex(r => r.DriverId);
        builder.HasIndex(r => r.ReportedAt);
    }
}
