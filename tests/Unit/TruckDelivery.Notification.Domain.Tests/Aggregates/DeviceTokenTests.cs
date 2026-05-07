using FluentAssertions;
using TruckDelivery.Notification.Domain.Aggregates;
using Xunit;

namespace TruckDelivery.Notification.Domain.Tests.Aggregates;

public sealed class DeviceTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_Should_SetProperties()
    {
        var token = DeviceToken.Create(UserId, "fcm-token-abc123", "Android");

        token.UserId.Should().Be(UserId);
        token.Token.Should().Be("fcm-token-abc123");
    }

    [Fact]
    public void Create_Should_NormalizePlatformToLowercase()
    {
        var tokenAndroid = DeviceToken.Create(UserId, "t1", "Android");
        var tokenIos = DeviceToken.Create(UserId, "t2", "IOS");

        tokenAndroid.Platform.Should().Be("android");
        tokenIos.Platform.Should().Be("ios");
    }

    [Fact]
    public void Create_Should_GenerateUniqueIds()
    {
        var t1 = DeviceToken.Create(UserId, "token-1", "android");
        var t2 = DeviceToken.Create(UserId, "token-2", "android");

        t1.Id.Should().NotBe(t2.Id);
    }

    [Fact]
    public void Create_Should_SetRegisteredAtToNow()
    {
        var before = DateTime.UtcNow;
        var token = DeviceToken.Create(UserId, "token", "ios");
        var after = DateTime.UtcNow;

        token.RegisteredAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Theory]
    [InlineData("android")]
    [InlineData("ios")]
    public void Create_Should_AcceptStandardPlatforms(string platform)
    {
        var token = DeviceToken.Create(UserId, "t", platform);

        token.Platform.Should().Be(platform.ToLowerInvariant());
    }
}
