using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Domain.Aggregates.Payment>
{
    public void Configure(EntityTypeBuilder<Domain.Aggregates.Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.OrderId).IsRequired();
        builder.Property(p => p.CustomerId).IsRequired();
        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(10).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
        builder.HasIndex(p => p.OrderId).IsUnique();
        builder.HasIndex(p => p.CustomerId);
    }
}
