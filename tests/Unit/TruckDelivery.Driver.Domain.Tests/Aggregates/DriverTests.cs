using FluentAssertions;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Domain.Tests.Aggregates;

public sealed class DriverTests
{
    private static readonly DateOnly ValidExpiry = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));
    private static readonly DateOnly ExpiredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
    private static readonly DateOnly TodayExpiry = DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Should_Succeed_WithValidInputs()
    {
        var result = CreateValidDriver();

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(DriverStatus.Offline);
        result.Value.VerificationStatus.Should().Be(DriverVerificationStatus.Draft);
        result.Value.TrustScore.Should().Be(70);
        result.Value.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(LicenseGrade.B1)]
    [InlineData(LicenseGrade.E)]
    public void Create_Should_Fail_WhenLicenseGradeIneligible(LicenseGrade grade)
    {
        var result = CreateDriverWithGrade(grade);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Driver.LicenseGrade");
    }

    [Fact]
    public void Create_Should_Fail_WhenLicenseExpired()
    {
        var result = CreateDriverWithExpiry(ExpiredDate);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Driver.LicenseExpiryDate");
    }

    [Fact]
    public void Create_Should_Succeed_WhenLicenseExpiresExactlyToday()
    {
        // License expiring today is still valid (L1 fix: < not <=)
        var result = CreateDriverWithExpiry(TodayExpiry);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_Should_NormalizeLicenseNumber_ToUpperCase()
    {
        var driver = CreateValidDriver().Value;

        driver.LicenseNumber.Should().Be("ABC123");
    }

    [Fact]
    public void Create_Should_NormalizeEmail_ToLowerCase()
    {
        var driver = CreateValidDriver().Value;

        driver.Email.Should().Be("driver@example.com");
    }

    [Fact]
    public void Create_Should_RaiseDriverRegisteredEvent()
    {
        var driver = CreateValidDriver().Value;

        driver.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "DriverRegisteredDomainEvent");
    }

    // ── SubmitDocuments ───────────────────────────────────────────────────────

    [Fact]
    public void SubmitDocuments_Should_Succeed_WhenDraft()
    {
        var driver = CreateValidDriver().Value;

        var result = driver.SubmitDocuments("p.jpg", "cf.jpg", "cb.jpg", "lf.jpg", "lb.jpg", "rf.jpg", "rb.jpg");

        result.IsSuccess.Should().BeTrue();
        driver.VerificationStatus.Should().Be(DriverVerificationStatus.PendingOcrVerification);
    }

    [Fact]
    public void SubmitDocuments_Should_Fail_WhenAlreadyOcrVerified()
    {
        var driver = CreateVerifiedDriver();

        var result = driver.SubmitDocuments("p.jpg", "cf.jpg", "cb.jpg", "lf.jpg", "lb.jpg", "rf.jpg", "rb.jpg");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Driver.Verification");
    }

    // ── ApplyOcrResult ────────────────────────────────────────────────────────

    [Fact]
    public void ApplyOcrResult_Should_SetOcrVerified_WhenHighConfidence()
    {
        var driver = CreatePendingVerificationDriver();

        driver.ApplyOcrResult(0.9f, DriverVerificationStatus.OcrVerified);

        driver.VerificationStatus.Should().Be(DriverVerificationStatus.OcrVerified);
        driver.OcrConfidenceScore.Should().Be(0.9f);
    }

    [Fact]
    public void ApplyOcrResult_Should_SetManualReview_WhenMidConfidence()
    {
        var driver = CreatePendingVerificationDriver();

        driver.ApplyOcrResult(0.75f, DriverVerificationStatus.ManualReview, "Blurry image");

        driver.VerificationStatus.Should().Be(DriverVerificationStatus.ManualReview);
        driver.VerificationNotes.Should().Be("Blurry image");
    }

    // ── AdminVerify / AdminReject ─────────────────────────────────────────────

    [Fact]
    public void AdminVerify_Should_Succeed_WhenManualReview()
    {
        var driver = CreatePendingVerificationDriver();
        driver.ApplyOcrResult(0.75f, DriverVerificationStatus.ManualReview);

        var result = driver.AdminVerify("Looks good");

        result.IsSuccess.Should().BeTrue();
        driver.VerificationStatus.Should().Be(DriverVerificationStatus.AdminVerified);
    }

    [Fact]
    public void AdminVerify_Should_Fail_WhenNotManualReview()
    {
        var driver = CreateValidDriver().Value;

        var result = driver.AdminVerify();

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Driver.Verification");
    }

    [Fact]
    public void AdminReject_Should_Succeed_WhenPendingOrManualReview()
    {
        var driver = CreatePendingVerificationDriver();

        var result = driver.AdminReject("Documents not clear");

        result.IsSuccess.Should().BeTrue();
        driver.VerificationStatus.Should().Be(DriverVerificationStatus.Rejected);
        driver.VerificationNotes.Should().Be("Documents not clear");
    }

    [Fact]
    public void AdminReject_Should_Fail_WhenAlreadyVerified()
    {
        var driver = CreateVerifiedDriver();

        var result = driver.AdminReject("too late");

        result.IsSuccess.Should().BeFalse();
    }

    // ── UpdateStatus ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateStatus_Should_Fail_WhenNotVerified()
    {
        var driver = CreateValidDriver().Value;

        var result = driver.UpdateStatus(DriverStatus.Available);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Driver.Verification");
    }

    [Fact]
    public void UpdateStatus_Should_Succeed_WhenOcrVerified()
    {
        var driver = CreateVerifiedDriver();

        var result = driver.UpdateStatus(DriverStatus.Available);

        result.IsSuccess.Should().BeTrue();
        driver.Status.Should().Be(DriverStatus.Available);
    }

    [Fact]
    public void UpdateStatus_Should_Fail_WhenDriverInactive()
    {
        var driver = CreateVerifiedDriver();
        driver.Deactivate();

        var result = driver.UpdateStatus(DriverStatus.Available);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void UpdateStatus_Should_RaiseStatusChangedEvent()
    {
        var driver = CreateVerifiedDriver();
        driver.ClearDomainEvents();

        driver.UpdateStatus(DriverStatus.Available);

        driver.DomainEvents.Should().ContainSingle(e => e.GetType().Name == "DriverStatusChangedDomainEvent");
    }

    // ── TrustScore ────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateTrustScore_Should_ClampAtZero()
    {
        var driver = CreateValidDriver().Value;

        driver.UpdateTrustScore(-200, "fraud");

        driver.TrustScore.Should().Be(0);
    }

    [Fact]
    public void UpdateTrustScore_Should_ClampAtOneHundred()
    {
        var driver = CreateValidDriver().Value;

        driver.UpdateTrustScore(200, "bonus");

        driver.TrustScore.Should().Be(100);
    }

    [Fact]
    public void ReportBreakdown_Should_DeductThreePointsFromTrustScore()
    {
        var driver = CreateValidDriver().Value;
        var beforeScore = driver.TrustScore;

        driver.ReportBreakdown(10.762, 106.660, FraudRiskLevel.Low);

        driver.TrustScore.Should().Be(beforeScore - 3);
    }

    [Fact]
    public void ReportBreakdown_Should_SetStatusOffline()
    {
        var driver = CreateVerifiedDriver();
        driver.UpdateStatus(DriverStatus.Available);

        driver.ReportBreakdown(10.762, 106.660, FraudRiskLevel.Low);

        driver.Status.Should().Be(DriverStatus.Offline);
    }

    [Fact]
    public void ReportBreakdown_Should_Fail_WhenDriverInactive()
    {
        var driver = CreateValidDriver().Value;
        driver.Deactivate();

        var result = driver.ReportBreakdown(10.762, 106.660, FraudRiskLevel.Low);

        result.IsSuccess.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Result<Driver> CreateValidDriver(
        LicenseGrade grade = LicenseGrade.C,
        DateOnly? expiry = null) =>
        Driver.Create(
            Guid.NewGuid(),
            "driver@example.com",
            "Nguyen",
            "Van A",
            "0901234567",
            "abc123",
            grade,
            expiry ?? ValidExpiry,
            new DateOnly(1990, 1, 1),
            "123 Le Loi, Q.1, HCM",
            "079123456789");

    private static Result<Driver> CreateDriverWithGrade(LicenseGrade grade) =>
        CreateValidDriver(grade: grade);

    private static Result<Driver> CreateDriverWithExpiry(DateOnly expiry) =>
        Driver.Create(
            Guid.NewGuid(), "driver@example.com", "A", "B",
            "0901234567", "abc123", LicenseGrade.C, expiry,
            new DateOnly(1990, 1, 1), "Address", "079123456789");

    private static Driver CreatePendingVerificationDriver()
    {
        var driver = CreateValidDriver().Value;
        driver.SubmitDocuments("p.jpg", "cf.jpg", "cb.jpg", "lf.jpg", "lb.jpg", "rf.jpg", "rb.jpg");
        return driver;
    }

    private static Driver CreateVerifiedDriver()
    {
        var driver = CreatePendingVerificationDriver();
        driver.ApplyOcrResult(0.9f, DriverVerificationStatus.OcrVerified);
        return driver;
    }
}
