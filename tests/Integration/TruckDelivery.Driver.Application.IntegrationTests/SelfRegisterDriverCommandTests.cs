using Xunit;
using FluentAssertions;
using TruckDelivery.Driver.Application.Commands.SelfRegisterDriver;
using TruckDelivery.Driver.Application.IntegrationTests.Fixtures;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Application.IntegrationTests;

[Collection("DriverIntegration")]
public sealed class SelfRegisterDriverCommandTests(DriverTestFixture fixture)
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));
    private static readonly DateOnly FutureDateReg = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
    private static readonly DateOnly BirthDate = new(1990, 6, 15);

    private static SelfRegisterDriverCommand BuildCommand(
        Guid? userId = null,
        string? idCardNumber = null,
        string? licensePlate = null,
        string? licenseNumber = null)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        return new SelfRegisterDriverCommand(
            UserId: userId ?? Guid.NewGuid(),
            Email: $"driver_{uniqueSuffix}@test.com",
            FirstName: "Nguyen",
            LastName: "Van B",
            PhoneNumber: "0900000002",
            IdCardNumber: idCardNumber ?? $"ID{uniqueSuffix}",
            DateOfBirth: BirthDate,
            Address: "456 Le Loi, Ha Noi",
            LicenseNumber: licenseNumber ?? $"LIC{uniqueSuffix}",
            LicenseGrade: LicenseGrade.C,
            LicenseExpiryDate: FutureDate,
            PortraitPhotoUrl: "https://minio/portrait.jpg",
            IdCardFrontUrl: "https://minio/id-front.jpg",
            IdCardBackUrl: "https://minio/id-back.jpg",
            LicenseFrontUrl: "https://minio/lic-front.jpg",
            LicenseBackUrl: "https://minio/lic-back.jpg",
            VehicleRegFrontUrl: "https://minio/reg-front.jpg",
            VehicleRegBackUrl: "https://minio/reg-back.jpg",
            LicensePlate: licensePlate ?? $"51G{uniqueSuffix[..4].ToUpper()}",
            Brand: "Toyota",
            Model: "Hilux",
            VehicleType: VehicleType.Truck3T,
            MaxWeightKg: 3000m,
            MaxVolumeCbm: 10m,
            LengthM: 4.5m,
            WidthM: 2.0m,
            HeightM: 2.2m,
            YearOfManufacture: 2020,
            RegistrationNumber: $"REG{uniqueSuffix}",
            RegistrationExpiryDate: FutureDateReg);
    }

    [Fact]
    public async Task Handle_Should_PersistDriverAndVehicle_InSingleTransaction()
    {
        var handler = new SelfRegisterDriverCommandHandler(
            fixture.DriverRepository,
            fixture.VehicleRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VerificationStatus.Should().Be("PendingOcrVerification");

        var driver = await fixture.DriverRepository.GetByIdAsync(result.Value.DriverId);
        var vehicle = await fixture.VehicleRepository.GetByIdAsync(result.Value.VehicleId);

        driver.Should().NotBeNull();
        vehicle.Should().NotBeNull();
        driver!.VerificationStatus.Should().Be(DriverVerificationStatus.PendingOcrVerification);
    }

    [Fact]
    public async Task Handle_Should_CreateOutboxEntry_ForDriverDocumentsSubmittedEvent()
    {
        var handler = new SelfRegisterDriverCommandHandler(
            fixture.DriverRepository,
            fixture.VehicleRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var result = await handler.Handle(BuildCommand(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        var unprocessed = await fixture.OutboxRepository.GetUnprocessedAsync(20);
        unprocessed.Should().Contain(m => m.EventType.Contains("DriverDocumentsSubmitted"));
    }

    [Fact]
    public async Task Handle_Should_Fail_WithConflict_WhenDuplicateIdCardNumber()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var sharedIdCard = $"DUP{uniqueSuffix}";

        var handler = new SelfRegisterDriverCommandHandler(
            fixture.DriverRepository,
            fixture.VehicleRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        // First registration succeeds
        var first = await handler.Handle(BuildCommand(idCardNumber: sharedIdCard), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Second registration with same ID card â†’ conflict
        var second = await handler.Handle(BuildCommand(idCardNumber: sharedIdCard), CancellationToken.None);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Driver.IdCard");
    }

    [Fact]
    public async Task Handle_Should_Fail_WithConflict_WhenDriverProfileAlreadyExists()
    {
        var userId = Guid.NewGuid();

        var handler = new SelfRegisterDriverCommandHandler(
            fixture.DriverRepository,
            fixture.VehicleRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var first = await handler.Handle(BuildCommand(userId: userId), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(BuildCommand(userId: userId), CancellationToken.None);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Driver");
    }

    [Fact]
    public async Task Handle_Should_Fail_WithConflict_WhenDuplicateLicensePlate()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var sharedPlate = $"51G{uniqueSuffix[..4].ToUpper()}";

        var handler = new SelfRegisterDriverCommandHandler(
            fixture.DriverRepository,
            fixture.VehicleRepository,
            fixture.UnitOfWork,
            fixture.OutboxRepository);

        var first = await handler.Handle(BuildCommand(licensePlate: sharedPlate), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(BuildCommand(licensePlate: sharedPlate), CancellationToken.None);
        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Vehicle");
    }
}
