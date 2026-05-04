using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Payment.Domain.Aggregates;

namespace TruckDelivery.Payment.Infrastructure.Persistence.EFCore.Configurations;

public sealed class EscrowPaymentConfiguration : IEntityTypeConfiguration<EscrowPayment>
{
    public void Configure(EntityTypeBuilder<EscrowPayment> builder)
    {
        builder.ToTable("EscrowPayments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ShipmentId).IsRequired();
        builder.Property(e => e.OrderId).IsRequired();
        builder.Property(e => e.OriginalDriverId).IsRequired();
        builder.Property(e => e.ReplacementDriverId).IsRequired();
        builder.Property(e => e.LockedAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.ResolutionNote).HasMaxLength(500);
        builder.Property(e => e.LockedAt).IsRequired();
        builder.Property(e => e.ResolvedAt);

        builder.HasIndex(e => e.ShipmentId).IsUnique();
        builder.HasIndex(e => e.Status);
        builder.Ignore(e => e.DomainEvents);
    }
}
