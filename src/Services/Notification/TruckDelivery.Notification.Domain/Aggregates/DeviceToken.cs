namespace TruckDelivery.Notification.Domain.Aggregates;

public sealed class DeviceToken
{
    private DeviceToken() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public string Platform { get; private set; } = default!; // "android" | "ios"
    public DateTime RegisteredAt { get; private set; }

    public static DeviceToken Create(Guid userId, string token, string platform)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            Platform = platform.ToLowerInvariant(),
            RegisteredAt = DateTime.UtcNow
        };
}
