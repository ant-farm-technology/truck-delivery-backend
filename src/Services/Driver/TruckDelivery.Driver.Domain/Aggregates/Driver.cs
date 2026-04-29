using TruckDelivery.Driver.Domain.Events;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Domain.Aggregates;

public sealed class Driver : AggregateRoot<Guid>
{
    private Driver() { }
    private Driver(Guid id) : base(id) { }

    public string Email { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string PhoneNumber { get; private set; } = default!;
    public string LicenseNumber { get; private set; } = default!;
    public DriverStatus Status { get; private set; }
    public Guid? CurrentVehicleId { get; private set; }
    public bool IsActive { get; private set; }
    public int TrustScore { get; private set; } = 70;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Result<Driver> Create(Guid userId, string email, string firstName, string lastName, string phoneNumber, string licenseNumber)
    {
        if (string.IsNullOrWhiteSpace(licenseNumber))
            return Result.Failure<Driver>(Error.Validation("Driver.LicenseNumber", "License number is required."));

        var driver = new Driver(userId)
        {
            Email = email.ToLowerInvariant(),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            LicenseNumber = licenseNumber.ToUpperInvariant(),
            Status = DriverStatus.Offline,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        driver.RaiseDomainEvent(new DriverRegisteredDomainEvent(driver.Id, driver.Email, driver.PhoneNumber));
        return Result.Success(driver);
    }

    public Result UpdateStatus(DriverStatus newStatus)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict("Driver", "Cannot change status of an inactive driver."));

        var oldStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new DriverStatusChangedDomainEvent(Id, oldStatus, newStatus));
        return Result.Success();
    }

    public Result AssignVehicle(Guid vehicleId)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict("Driver", "Inactive driver cannot be assigned a vehicle."));

        CurrentVehicleId = vehicleId;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void UnassignVehicle()
    {
        CurrentVehicleId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        Status = DriverStatus.Offline;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result UpdateTrustScore(int delta, string reason)
    {
        var previous = TrustScore;
        TrustScore = Math.Clamp(TrustScore + delta, 0, 100);
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new TrustScoreUpdatedDomainEvent(Id, TrustScore, TrustScore - previous, reason));
        return Result.Success();
    }

    public Result ReportBreakdown(double latitude, double longitude, FraudRiskLevel riskLevel)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict("Driver.Breakdown", "Inactive driver cannot report breakdown."));

        UpdateTrustScore(-3, "vehicle_breakdown");
        var oldStatus = Status;
        Status = DriverStatus.Offline;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new DriverBreakdownReportedDomainEvent(
            Id, CurrentVehicleId, latitude, longitude, [], TrustScore, riskLevel));
        RaiseDomainEvent(new DriverStatusChangedDomainEvent(Id, oldStatus, DriverStatus.Offline));
        return Result.Success();
    }
}
