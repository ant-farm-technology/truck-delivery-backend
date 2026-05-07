using FluentAssertions;
using TruckDelivery.Tracking.Domain.Aggregates;
using TruckDelivery.Tracking.Domain.Events;
using Xunit;

namespace TruckDelivery.Tracking.Domain.Tests.Aggregates;

public sealed class TrackingSessionTests
{
    private static readonly Guid ShipmentId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid DriverId = Guid.NewGuid();

    // ── Start ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Start_Should_SetActiveAndBindIds()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        session.IsActive.Should().BeTrue();
        session.ShipmentId.Should().Be(ShipmentId);
        session.OrderId.Should().Be(OrderId);
        session.DriverId.Should().Be(DriverId);
    }

    [Fact]
    public void Start_Should_SetStartedAtToNow()
    {
        var before = DateTime.UtcNow;
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        var after = DateTime.UtcNow;

        session.StartedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Start_Should_HaveNullLocation_Initially()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        session.LastLatitude.Should().BeNull();
        session.LastLongitude.Should().BeNull();
        session.LastUpdatedAt.Should().BeNull();
        session.EndedAt.Should().BeNull();
    }

    [Fact]
    public void Start_Should_RaiseTrackingSessionStartedEvent()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        session.DomainEvents.Should().ContainSingle(e => e is TrackingSessionStartedDomainEvent);
        var evt = (TrackingSessionStartedDomainEvent)session.DomainEvents.Single();
        evt.ShipmentId.Should().Be(ShipmentId);
        evt.DriverId.Should().Be(DriverId);
        evt.SessionId.Should().Be(session.Id);
    }

    [Fact]
    public void Start_Should_GenerateUniqueIds()
    {
        var s1 = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        var s2 = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        s1.Id.Should().NotBe(s2.Id);
    }

    // ── UpdateLocation ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateLocation_Should_SetLastCoordinates()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        session.UpdateLocation(10.7769, 106.7009);

        session.LastLatitude.Should().Be(10.7769);
        session.LastLongitude.Should().Be(106.7009);
    }

    [Fact]
    public void UpdateLocation_Should_SetLastUpdatedAt()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        var before = DateTime.UtcNow;

        session.UpdateLocation(10.7769, 106.7009);

        var after = DateTime.UtcNow;
        session.LastUpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UpdateLocation_Should_RaiseLocationUpdatedEvent()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        session.ClearDomainEvents();

        session.UpdateLocation(10.7769, 106.7009);

        session.DomainEvents.Should().ContainSingle(e => e is LocationUpdatedDomainEvent);
        var evt = (LocationUpdatedDomainEvent)session.DomainEvents.Single();
        evt.ShipmentId.Should().Be(ShipmentId);
        evt.DriverId.Should().Be(DriverId);
        evt.Latitude.Should().Be(10.7769);
        evt.Longitude.Should().Be(106.7009);
    }

    [Fact]
    public void UpdateLocation_Should_OverwritePreviousCoordinates()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        session.UpdateLocation(10.0, 106.0);

        session.UpdateLocation(21.0285, 105.8542);

        session.LastLatitude.Should().Be(21.0285);
        session.LastLongitude.Should().Be(105.8542);
    }

    [Theory]
    [InlineData(-90.0, 0.0)]
    [InlineData(90.0, 0.0)]
    [InlineData(0.0, -180.0)]
    [InlineData(0.0, 180.0)]
    public void UpdateLocation_Should_AcceptBoundaryCoordinates(double lat, double lng)
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        session.UpdateLocation(lat, lng);

        session.LastLatitude.Should().Be(lat);
        session.LastLongitude.Should().Be(lng);
    }

    [Fact]
    public void UpdateLocation_Multiple_Should_RaiseMultipleEvents()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        session.ClearDomainEvents();

        session.UpdateLocation(10.0, 106.0);
        session.UpdateLocation(10.1, 106.1);
        session.UpdateLocation(10.2, 106.2);

        session.DomainEvents.Should().HaveCount(3);
        session.DomainEvents.Should().AllSatisfy(e => e.Should().BeOfType<LocationUpdatedDomainEvent>());
    }

    // ── Stop ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Stop_Should_SetInactive()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);

        session.Stop();

        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Stop_Should_SetEndedAt()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        var before = DateTime.UtcNow;

        session.Stop();

        var after = DateTime.UtcNow;
        session.EndedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Stop_Should_PreserveLastLocation()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        session.UpdateLocation(10.7769, 106.7009);

        session.Stop();

        session.LastLatitude.Should().Be(10.7769);
        session.LastLongitude.Should().Be(106.7009);
    }

    // ── Full lifecycle ─────────────────────────────────────────────────────────

    [Fact]
    public void FullLifecycle_StartUpdateStop_ShouldSucceed()
    {
        var session = TrackingSession.Start(ShipmentId, OrderId, DriverId);
        session.UpdateLocation(10.0, 106.0);
        session.UpdateLocation(10.5, 106.5);

        session.Stop();

        session.IsActive.Should().BeFalse();
        session.EndedAt.Should().NotBeNull();
        session.StartedAt.Should().BeBefore(session.EndedAt!.Value);
        session.LastLatitude.Should().Be(10.5);
    }
}
