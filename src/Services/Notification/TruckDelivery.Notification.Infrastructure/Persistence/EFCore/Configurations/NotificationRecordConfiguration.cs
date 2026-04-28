using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruckDelivery.Notification.Domain.Aggregates;

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore.Configurations;

public sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).IsRequired();
        builder.Property(n => n.RecipientId).IsRequired();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000).IsRequired();
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.FailureReason).HasMaxLength(500);
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.SentAt);
        builder.HasIndex(n => n.RecipientId);
        builder.HasIndex(n => n.CreatedAt);
    }
}
