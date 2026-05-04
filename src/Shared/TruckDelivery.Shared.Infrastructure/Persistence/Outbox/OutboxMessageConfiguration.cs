using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessageConfiguration(string schema) : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", schema);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PartitionKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => x.ProcessedAt);
        builder.HasIndex(x => new { x.ProcessedAt, x.RetryCount });
    }
}
