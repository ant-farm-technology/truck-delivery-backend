using FluentAssertions;
using TruckDelivery.Analytics.Domain.Documents;

namespace TruckDelivery.Analytics.Domain.Tests.Documents;

public sealed class BreakdownIncidentTests
{
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid ShipmentId = Guid.NewGuid();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Should_SetRequiredFields()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Low", 10.7769, 106.7009);

        incident.DriverId.Should().Be(DriverId);
        incident.VehicleId.Should().Be(VehicleId);
        incident.FraudRiskLevel.Should().Be("Low");
        incident.Latitude.Should().Be(10.7769);
        incident.Longitude.Should().Be(106.7009);
    }

    [Fact]
    public void Create_Should_SetUnresolvedState()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Low", 0, 0);

        incident.IsResolved.Should().BeFalse();
        incident.IsSuccessfullyReassigned.Should().BeFalse();
        incident.ResolvedAt.Should().BeNull();
        incident.ShipmentId.Should().BeNull();
        incident.RecoveryTimeMinutes.Should().BeNull();
    }

    [Fact]
    public void Create_Should_SetReportedAtToNow()
    {
        var before = DateTime.UtcNow;
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Medium", 0, 0);
        var after = DateTime.UtcNow;

        incident.ReportedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_Should_GenerateUniqueIds()
    {
        var i1 = BreakdownIncident.Create(DriverId, VehicleId, "Low", 0, 0);
        var i2 = BreakdownIncident.Create(DriverId, VehicleId, "Low", 0, 0);

        i1.Id.Should().NotBe(i2.Id);
    }

    [Fact]
    public void Create_Should_AcceptNullVehicleId()
    {
        var incident = BreakdownIncident.Create(DriverId, null, "Unknown", 10.0, 106.0);

        incident.VehicleId.Should().BeNull();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("Low")]
    [InlineData("Medium")]
    [InlineData("High")]
    [InlineData("Confirmed")]
    public void Create_Should_AcceptAllFraudRiskLevels(string riskLevel)
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, riskLevel, 0, 0);

        incident.FraudRiskLevel.Should().Be(riskLevel);
    }

    // ── MarkResolved ──────────────────────────────────────────────────────────

    [Fact]
    public void MarkResolved_WithSuccessfulReassignment_Should_SetAllFields()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Low", 0, 0);

        incident.MarkResolved(ShipmentId, isReassigned: true);

        incident.IsResolved.Should().BeTrue();
        incident.IsSuccessfullyReassigned.Should().BeTrue();
        incident.ShipmentId.Should().Be(ShipmentId);
        incident.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkResolved_WithFailedReassignment_Should_SetIsResolvedButNotReassigned()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "High", 0, 0);

        incident.MarkResolved(ShipmentId, isReassigned: false);

        incident.IsResolved.Should().BeTrue();
        incident.IsSuccessfullyReassigned.Should().BeFalse();
    }

    [Fact]
    public void MarkResolved_WithSuccessfulReassignment_Should_CalculateRecoveryTime()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Low", 0, 0);

        incident.MarkResolved(ShipmentId, isReassigned: true);

        // RecoveryTimeMinutes is (ResolvedAt - ReportedAt).TotalMinutes rounded to int
        // Since both happen nearly simultaneously in tests, it should be 0 or very small
        incident.RecoveryTimeMinutes.Should().NotBeNull();
        incident.RecoveryTimeMinutes.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void MarkResolved_WithFailedReassignment_Should_NotSetRecoveryTime()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "High", 0, 0);

        incident.MarkResolved(ShipmentId, isReassigned: false);

        incident.RecoveryTimeMinutes.Should().BeNull();
    }

    [Fact]
    public void MarkResolved_Should_SetResolvedAtToNow()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Low", 0, 0);
        var before = DateTime.UtcNow;

        incident.MarkResolved(ShipmentId, isReassigned: true);

        var after = DateTime.UtcNow;
        incident.ResolvedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ── Invariant: resolved after reported ────────────────────────────────────

    [Fact]
    public void MarkResolved_Should_HaveResolvedAtAfterReportedAt()
    {
        var incident = BreakdownIncident.Create(DriverId, VehicleId, "Medium", 0, 0);

        incident.MarkResolved(ShipmentId, isReassigned: true);

        incident.ResolvedAt.Should().BeOnOrAfter(incident.ReportedAt);
    }
}
