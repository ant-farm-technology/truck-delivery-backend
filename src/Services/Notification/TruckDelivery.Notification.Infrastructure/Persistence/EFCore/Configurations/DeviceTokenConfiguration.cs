using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Notification.Domain.Aggregates;

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore.Configurations;

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Token).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RegisteredAt).IsRequired();

        // One token per user+platform — upsert replaces existing
        builder.HasIndex(x => new { x.UserId, x.Platform }).IsUnique();
    }
}
