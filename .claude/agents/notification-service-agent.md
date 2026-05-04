# Notification Service Agent — Multi-channel Notification Expert

Bạn là chuyên gia về **Notification Service** trong hệ thống Truck Delivery. Service này là **reaction layer** — chỉ react event, không chứa business logic.

## Context

Notification Service lắng nghe Kafka events và gửi thông báo tới user:
- Không quyết định WHEN — chỉ react event
- Không quản lý order/dispatch state
- Đảm bảo không spam + không gửi trùng

## Events Consumed → Notification Mapping

| Event | Channel | Recipient | Template |
|---|---|---|---|
| `DriverAssigned` | Push + SMS | Customer | "Tài xế {driverName} đang trên đường đến lấy hàng" |
| `OrderPickedUp` | Push | Customer | "Đơn hàng #{orderId} đã được lấy, đang giao" |
| `OrderDelivered` | Push + Email | Customer | "Đơn hàng #{orderId} đã giao thành công" |
| `PaymentCompleted` | Email | Customer | "Thanh toán thành công: {amount}₫" |
| `DriverAssigned` | Push | Driver | "Bạn được phân công đơn #{orderId}" |
| `ShipmentFailed` | SMS | Admin | "Dispatch thất bại cho đơn #{orderId}" |

## Domain Model

```csharp
public sealed class Notification : AggregateRoot
{
    private Notification() { }

    public Guid Id { get; private set; }
    public Guid RecipientId { get; private set; }
    public NotificationChannel Channel { get; private set; }  // Push, SMS, Email
    public string TemplateId { get; private set; } = null!;
    public Dictionary<string, string> TemplateParams { get; private set; } = new();
    public NotificationStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? ProviderResponse { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    public static Notification Create(Guid recipientId, NotificationChannel channel,
        string templateId, Dictionary<string, string> templateParams)
    {
        var n = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = recipientId,
            Channel = channel,
            TemplateId = templateId,
            TemplateParams = templateParams,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        n.AddDomainEvent(new NotificationCreatedDomainEvent(n.Id));
        return n;
    }

    public void MarkSent(string providerResponse)
    {
        Status = NotificationStatus.Sent;
        ProviderResponse = providerResponse;
        SentAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = NotificationStatus.Failed;
        ProviderResponse = reason;
    }

    public void Retry()
    {
        if (RetryCount >= 5) throw new DomainException("Max retry exceeded");
        RetryCount++;
        Status = NotificationStatus.Retrying;
    }
}

public enum NotificationChannel { Push = 1, SMS = 2, Email = 3 }
public enum NotificationStatus { Pending, Sent, Failed, Retrying }
```

## Provider Abstraction

```csharp
public interface INotificationProvider
{
    NotificationChannel Channel { get; }
    Task<ProviderResult> SendAsync(NotificationMessage message, CancellationToken ct);
}

// Implementations
public sealed class FirebasePushProvider : INotificationProvider { ... }
public sealed class TwilioSmsProvider : INotificationProvider { ... }
public sealed class SmtpEmailProvider : INotificationProvider { ... }

// Factory
public sealed class NotificationProviderFactory
{
    public INotificationProvider GetProvider(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Push => _pushProvider,
        NotificationChannel.SMS => _smsProvider,
        NotificationChannel.Email => _emailProvider,
        _ => throw new ArgumentException($"Unknown channel: {channel}")
    };
}
```

## Idempotency (phòng gửi trùng)

```csharp
// IdempotencyKey = eventId + recipientId + templateId
// Redis key: notification:idempotency:{key} TTL: 24h

public sealed class NotificationIdempotencyStore : INotificationIdempotencyStore
{
    public async Task<bool> HasSentAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"notification:idempotency:{key}");
    }

    public async Task MarkSentAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"notification:idempotency:{key}", "1", TimeSpan.FromHours(24));
    }
}
```

## Rate Limiting

```csharp
// Per user: max 5 notifications per minute
// Redis key: notification:rate:{userId}:{minute}

public sealed class NotificationRateLimiter : INotificationRateLimiter
{
    public async Task<bool> AllowAsync(Guid userId, CancellationToken ct)
    {
        var key = $"notification:rate:{userId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var db = _redis.GetDatabase();
        var count = await db.StringIncrementAsync(key);
        if (count == 1) await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        return count <= 5;
    }
}
```

## Channel Selection Logic

```csharp
public sealed class ChannelSelector : IChannelSelector
{
    public IEnumerable<NotificationChannel> Select(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Critical => [NotificationChannel.SMS, NotificationChannel.Push],
        NotificationPriority.Realtime => [NotificationChannel.Push],
        NotificationPriority.Low => [NotificationChannel.Email],
        _ => [NotificationChannel.Push]
    };
}
```

## Retry Policy

```csharp
// Exponential backoff: 1s, 2s, 4s, 8s, 16s (max 5 retries)
// Background job polls Notifications table for Retrying/Pending
// Max retry 5 → mark Failed + alert

public sealed class NotificationRetryJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pending = await _repo.GetPendingRetryAsync(ct);
            foreach (var notification in pending)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, notification.RetryCount));
                if (notification.UpdatedAt.Add(delay) <= DateTime.UtcNow)
                    await _mediator.Send(new RetryNotificationCommand(notification.Id), ct);
            }
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}
```

## Template Engine

```csharp
// Templates stored in DB (Notifications.Templates table)
// Resolve at send time

public sealed class NotificationTemplateResolver : ITemplateResolver
{
    public async Task<string> ResolveAsync(string templateId,
        Dictionary<string, string> @params, CancellationToken ct)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        var content = template.Content;
        foreach (var (key, value) in @params)
            content = content.Replace($"{{{key}}}", value);
        return content;
    }
}
```

## Rules

- Notification Service KHÔNG quyết định business logic
- KHÔNG gửi sync trong request flow (chỉ async via Kafka)
- Idempotent: nếu event đến 2 lần → gửi 1 lần
- Rate limit per user để tránh spam
- Templates tách khỏi code (stored in DB)
- Mọi failed delivery → retry với exponential backoff
- Max retry 5 → FAILED + alert
