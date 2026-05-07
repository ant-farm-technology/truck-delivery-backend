using FluentAssertions;
using TruckDelivery.Notification.Domain.Aggregates;
using Xunit;

namespace TruckDelivery.Notification.Domain.Tests.Aggregates;

public sealed class NotificationRecordTests
{
    private static readonly Guid RecipientId = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Should_SetPendingStatus()
    {
        var record = NotificationRecord.Create(
            RecipientId, NotificationType.DriverAssigned,
            NotificationChannel.Push, "Driver assigned", "Your driver is on the way");

        record.Status.Should().Be(NotificationStatus.Pending);
        record.RecipientId.Should().Be(RecipientId);
        record.Type.Should().Be(NotificationType.DriverAssigned);
        record.Channel.Should().Be(NotificationChannel.Push);
        record.Title.Should().Be("Driver assigned");
        record.Body.Should().Be("Your driver is on the way");
    }

    [Fact]
    public void Create_Should_SetCreatedAtToNow()
    {
        var before = DateTime.UtcNow;
        var record = NotificationRecord.Create(
            RecipientId, NotificationType.PaymentCompleted,
            NotificationChannel.Email, "Payment done", "Payment received");
        var after = DateTime.UtcNow;

        record.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_Should_GenerateUniqueIds()
    {
        var r1 = NotificationRecord.Create(RecipientId, NotificationType.ShipmentDelivered,
            NotificationChannel.Push, "T", "B");
        var r2 = NotificationRecord.Create(RecipientId, NotificationType.ShipmentDelivered,
            NotificationChannel.Push, "T", "B");

        r1.Id.Should().NotBe(r2.Id);
    }

    [Fact]
    public void Create_Should_HaveNullSentAt_Initially()
    {
        var record = NotificationRecord.Create(RecipientId, NotificationType.DriverAssigned,
            NotificationChannel.Sms, "Title", "Body");

        record.SentAt.Should().BeNull();
        record.FailureReason.Should().BeNull();
    }

    // ── MarkSent ──────────────────────────────────────────────────────────────

    [Fact]
    public void MarkSent_Should_SetSentStatus()
    {
        var record = CreatePendingNotification();

        record.MarkSent();

        record.Status.Should().Be(NotificationStatus.Sent);
    }

    [Fact]
    public void MarkSent_Should_SetSentAtTimestamp()
    {
        var record = CreatePendingNotification();
        var before = DateTime.UtcNow;

        record.MarkSent();

        var after = DateTime.UtcNow;
        record.SentAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void MarkSent_Should_PreserveRecipientAndType()
    {
        var record = NotificationRecord.Create(
            RecipientId, NotificationType.ShipmentPickedUp,
            NotificationChannel.Push, "T", "B");

        record.MarkSent();

        record.RecipientId.Should().Be(RecipientId);
        record.Type.Should().Be(NotificationType.ShipmentPickedUp);
    }

    // ── MarkFailed ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkFailed_Should_SetFailedStatus()
    {
        var record = CreatePendingNotification();

        record.MarkFailed("FCM token invalid");

        record.Status.Should().Be(NotificationStatus.Failed);
    }

    [Fact]
    public void MarkFailed_Should_StoreFailureReason()
    {
        var record = CreatePendingNotification();

        record.MarkFailed("Device not registered");

        record.FailureReason.Should().Be("Device not registered");
    }

    [Fact]
    public void MarkFailed_Should_NotSetSentAt()
    {
        var record = CreatePendingNotification();

        record.MarkFailed("error");

        record.SentAt.Should().BeNull();
    }

    // ── Channel variations ────────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationChannel.Push)]
    [InlineData(NotificationChannel.Sms)]
    [InlineData(NotificationChannel.Email)]
    public void Create_Should_AcceptAllChannels(NotificationChannel channel)
    {
        var record = NotificationRecord.Create(RecipientId, NotificationType.PaymentCompleted,
            channel, "T", "B");

        record.Channel.Should().Be(channel);
        record.Status.Should().Be(NotificationStatus.Pending);
    }

    // ── Type variations ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationType.DriverAssigned)]
    [InlineData(NotificationType.ShipmentPickedUp)]
    [InlineData(NotificationType.ShipmentDelivered)]
    [InlineData(NotificationType.PaymentCompleted)]
    [InlineData(NotificationType.PaymentFailed)]
    [InlineData(NotificationType.ShipmentStarted)]
    [InlineData(NotificationType.DriverManualReviewRequired)]
    public void Create_Should_AcceptAllNotificationTypes(NotificationType type)
    {
        var record = NotificationRecord.Create(RecipientId, type,
            NotificationChannel.Push, "Title", "Body");

        record.Type.Should().Be(type);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static NotificationRecord CreatePendingNotification() =>
        NotificationRecord.Create(RecipientId, NotificationType.DriverAssigned,
            NotificationChannel.Push, "Title", "Body");
}
