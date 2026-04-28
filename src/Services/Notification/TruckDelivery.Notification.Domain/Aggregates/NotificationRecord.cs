using TruckDelivery.Shared.Common.Domain;

namespace TruckDelivery.Notification.Domain.Aggregates;

public sealed class NotificationRecord : AggregateRoot<Guid>
{
    private NotificationRecord() { }

    private NotificationRecord(Guid id, Guid recipientId, NotificationType type,
        NotificationChannel channel, string title, string body) : base(id)
    {
        RecipientId = recipientId;
        Type = type;
        Channel = channel;
        Title = title;
        Body = body;
        Status = NotificationStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static NotificationRecord Create(Guid recipientId, NotificationType type,
        NotificationChannel channel, string title, string body)
        => new(Guid.NewGuid(), recipientId, type, channel, title, body);

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = NotificationStatus.Failed;
        FailureReason = reason;
    }

    public Guid RecipientId { get; private set; }
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public NotificationStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
}
