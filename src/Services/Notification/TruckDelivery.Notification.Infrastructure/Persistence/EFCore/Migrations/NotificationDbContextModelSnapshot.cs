using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TruckDelivery.Notification.Infrastructure.Persistence.EFCore;

#nullable disable

namespace TruckDelivery.Notification.Infrastructure.Persistence.EFCore.Migrations
{
    [DbContext(typeof(NotificationDbContext))]
    partial class NotificationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("TruckDelivery.Notification.Domain.Aggregates.DeviceToken", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("char(36)");

                b.Property<string>("Platform")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("varchar(20)");

                b.Property<DateTime>("RegisteredAt")
                    .HasColumnType("datetime(6)");

                b.Property<string>("Token")
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasColumnType("varchar(500)");

                b.Property<Guid>("UserId")
                    .HasColumnType("char(36)");

                b.HasKey("Id");

                b.HasIndex("UserId", "Platform")
                    .IsUnique()
                    .HasDatabaseName("IX_device_tokens_UserId_Platform");

                b.ToTable("device_tokens");
            });

            modelBuilder.Entity("TruckDelivery.Notification.Domain.Aggregates.NotificationRecord", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("char(36)");

                b.Property<string>("Body")
                    .IsRequired()
                    .HasMaxLength(2000)
                    .HasColumnType("varchar(2000)");

                b.Property<string>("Channel")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("varchar(20)");

                b.Property<DateTime>("CreatedAt")
                    .HasColumnType("datetime(6)");

                b.Property<string>("FailureReason")
                    .HasMaxLength(500)
                    .HasColumnType("varchar(500)");

                b.Property<Guid>("RecipientId")
                    .HasColumnType("char(36)");

                b.Property<DateTime?>("SentAt")
                    .HasColumnType("datetime(6)");

                b.Property<string>("Status")
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasColumnType("varchar(20)");

                b.Property<string>("Title")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("varchar(200)");

                b.Property<string>("Type")
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasColumnType("varchar(50)");

                b.HasKey("Id");

                b.HasIndex("CreatedAt")
                    .HasDatabaseName("IX_Notifications_CreatedAt");

                b.HasIndex("RecipientId")
                    .HasDatabaseName("IX_Notifications_RecipientId");

                b.ToTable("Notifications");
            });

            modelBuilder.Entity("TruckDelivery.Shared.Infrastructure.Persistence.Outbox.OutboxMessage", b =>
            {
                b.Property<Guid>("Id")
                    .ValueGeneratedOnAdd()
                    .HasColumnType("char(36)");

                b.Property<string>("EventType")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("varchar(200)");

                b.Property<string>("LastError")
                    .HasMaxLength(1000)
                    .HasColumnType("varchar(1000)");

                b.Property<DateTime>("OccurredAt")
                    .HasColumnType("datetime(6)");

                b.Property<string>("PartitionKey")
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasColumnType("varchar(100)");

                b.Property<string>("Payload")
                    .IsRequired()
                    .HasColumnType("longtext");

                b.Property<DateTime?>("ProcessedAt")
                    .HasColumnType("datetime(6)");

                b.Property<int>("RetryCount")
                    .HasColumnType("int");

                b.Property<string>("Topic")
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnType("varchar(200)");

                b.HasKey("Id");

                b.HasIndex("ProcessedAt")
                    .HasDatabaseName("IX_OutboxMessages_ProcessedAt");

                b.HasIndex("ProcessedAt", "RetryCount")
                    .HasDatabaseName("IX_OutboxMessages_ProcessedAt_RetryCount");

                b.ToTable("OutboxMessages");
            });
#pragma warning restore 612, 618
        }
    }
}
