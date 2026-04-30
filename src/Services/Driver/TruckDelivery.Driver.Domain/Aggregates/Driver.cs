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
    public LicenseGrade LicenseGrade { get; private set; }
    public DateOnly LicenseExpiryDate { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public string Address { get; private set; } = default!;
    public string IdCardNumber { get; private set; } = default!;

    // Document photo URLs (7 required for verification)
    public string? PortraitPhotoUrl { get; private set; }
    public string? IdCardFrontUrl { get; private set; }
    public string? IdCardBackUrl { get; private set; }
    public string? LicenseFrontUrl { get; private set; }
    public string? LicenseBackUrl { get; private set; }
    public string? VehicleRegFrontUrl { get; private set; }
    public string? VehicleRegBackUrl { get; private set; }

    public DriverVerificationStatus VerificationStatus { get; private set; }
    public float? OcrConfidenceScore { get; private set; }
    public string? VerificationNotes { get; private set; }

    public DriverStatus Status { get; private set; }
    public Guid? CurrentVehicleId { get; private set; }
    public bool IsActive { get; private set; }
    public int TrustScore { get; private set; } = 70;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Result<Driver> Create(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        string phoneNumber,
        string licenseNumber,
        LicenseGrade licenseGrade,
        DateOnly licenseExpiryDate,
        DateOnly dateOfBirth,
        string address,
        string idCardNumber)
    {
        if (string.IsNullOrWhiteSpace(licenseNumber))
            return Result.Failure<Driver>(Error.Validation("Driver.LicenseNumber", "License number is required."));
        if (string.IsNullOrWhiteSpace(idCardNumber))
            return Result.Failure<Driver>(Error.Validation("Driver.IdCardNumber", "ID card number is required."));
        if (licenseGrade == LicenseGrade.B1 || licenseGrade == LicenseGrade.E)
            return Result.Failure<Driver>(Error.Validation("Driver.LicenseGrade", "License grade is not eligible for freight transport."));
        if (licenseExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow))
            return Result.Failure<Driver>(Error.Validation("Driver.LicenseExpiryDate", "License has expired."));

        var driver = new Driver(userId)
        {
            Email = email.ToLowerInvariant(),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            LicenseNumber = licenseNumber.ToUpperInvariant(),
            LicenseGrade = licenseGrade,
            LicenseExpiryDate = licenseExpiryDate,
            DateOfBirth = dateOfBirth,
            Address = address,
            IdCardNumber = idCardNumber,
            VerificationStatus = DriverVerificationStatus.Draft,
            Status = DriverStatus.Offline,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        driver.RaiseDomainEvent(new DriverRegisteredDomainEvent(driver.Id, driver.Email, driver.PhoneNumber));
        return Result.Success(driver);
    }

    public Result SubmitDocuments(
        string portraitPhotoUrl,
        string idCardFrontUrl,
        string idCardBackUrl,
        string licenseFrontUrl,
        string licenseBackUrl,
        string vehicleRegFrontUrl,
        string vehicleRegBackUrl)
    {
        if (VerificationStatus == DriverVerificationStatus.OcrVerified ||
            VerificationStatus == DriverVerificationStatus.AdminVerified)
            return Result.Failure(Error.Conflict("Driver.Verification", "Driver is already verified."));

        PortraitPhotoUrl = portraitPhotoUrl;
        IdCardFrontUrl = idCardFrontUrl;
        IdCardBackUrl = idCardBackUrl;
        LicenseFrontUrl = licenseFrontUrl;
        LicenseBackUrl = licenseBackUrl;
        VehicleRegFrontUrl = vehicleRegFrontUrl;
        VehicleRegBackUrl = vehicleRegBackUrl;
        VerificationStatus = DriverVerificationStatus.PendingOcrVerification;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void ApplyOcrResult(float confidenceScore, DriverVerificationStatus status, string? notes = null)
    {
        OcrConfidenceScore = confidenceScore;
        VerificationStatus = status;
        VerificationNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result AdminVerify(string? notes = null)
    {
        if (VerificationStatus != DriverVerificationStatus.ManualReview)
            return Result.Failure(Error.Conflict("Driver.Verification", "Only drivers in ManualReview can be admin-verified."));

        VerificationStatus = DriverVerificationStatus.AdminVerified;
        VerificationNotes = notes;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result AdminReject(string reason)
    {
        if (VerificationStatus == DriverVerificationStatus.OcrVerified ||
            VerificationStatus == DriverVerificationStatus.AdminVerified)
            return Result.Failure(Error.Conflict("Driver.Verification", "Cannot reject an already verified driver."));

        VerificationStatus = DriverVerificationStatus.Rejected;
        VerificationNotes = reason;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public Result UpdateStatus(DriverStatus newStatus)
    {
        if (!IsActive)
            return Result.Failure(Error.Conflict("Driver", "Cannot change status of an inactive driver."));

        // Driver must be verified before going Available
        if (newStatus == DriverStatus.Available &&
            VerificationStatus != DriverVerificationStatus.OcrVerified &&
            VerificationStatus != DriverVerificationStatus.AdminVerified)
            return Result.Failure(Error.Validation("Driver.Verification", "Driver must be verified before becoming available."));

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
