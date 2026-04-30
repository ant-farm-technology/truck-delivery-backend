using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TruckDelivery.Driver.Infrastructure.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Driver>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Driver> builder)
    {
        builder.ToTable("drivers");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Email).HasMaxLength(255).IsRequired();
        builder.Property(d => d.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LastName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(d => d.LicenseNumber).HasMaxLength(50).IsRequired();
        builder.Property(d => d.LicenseGrade).HasConversion<int>().IsRequired();
        builder.Property(d => d.LicenseExpiryDate).IsRequired();
        builder.Property(d => d.DateOfBirth).IsRequired();
        builder.Property(d => d.Address).HasMaxLength(500).IsRequired();
        builder.Property(d => d.IdCardNumber).HasMaxLength(20).IsRequired();

        builder.Property(d => d.PortraitPhotoUrl).HasMaxLength(1000);
        builder.Property(d => d.IdCardFrontUrl).HasMaxLength(1000);
        builder.Property(d => d.IdCardBackUrl).HasMaxLength(1000);
        builder.Property(d => d.LicenseFrontUrl).HasMaxLength(1000);
        builder.Property(d => d.LicenseBackUrl).HasMaxLength(1000);
        builder.Property(d => d.VehicleRegFrontUrl).HasMaxLength(1000);
        builder.Property(d => d.VehicleRegBackUrl).HasMaxLength(1000);

        builder.Property(d => d.VerificationStatus).HasConversion<int>().IsRequired().HasDefaultValue(0);
        builder.Property(d => d.OcrConfidenceScore);
        builder.Property(d => d.VerificationNotes).HasMaxLength(1000);

        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.IsActive).IsRequired();
        builder.Property(d => d.TrustScore).IsRequired().HasDefaultValue(70);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        builder.HasIndex(d => d.Email).IsUnique();
        builder.HasIndex(d => d.LicenseNumber).IsUnique();
        builder.HasIndex(d => d.IdCardNumber).IsUnique();
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.VerificationStatus);
    }
}
