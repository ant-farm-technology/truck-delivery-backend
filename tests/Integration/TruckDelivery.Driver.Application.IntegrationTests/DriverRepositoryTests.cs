using FluentAssertions;
using TruckDelivery.Driver.Application.IntegrationTests.Fixtures;
using TruckDelivery.Driver.Domain.ValueObjects;

namespace TruckDelivery.Driver.Application.IntegrationTests;

[Collection("DriverIntegration")]
public sealed class DriverRepositoryTests(DriverTestFixture fixture)
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));
    private static readonly DateOnly BirthDate = new(1990, 1, 1);

    private static Domain.Aggregates.Driver BuildDriver(string? idCard = null, string? license = null)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        return Domain.Aggregates.Driver.Create(
            userId: Guid.NewGuid(),
            email: $"driver_{uniqueSuffix}@test.com",
            firstName: "Nguyen",
            lastName: "Van A",
            phoneNumber: "0900000001",
            licenseNumber: license ?? $"LIC{uniqueSuffix}",
            licenseGrade: LicenseGrade.C,
            licenseExpiryDate: FutureDate,
            dateOfBirth: BirthDate,
            address: "123 Nguyen Trai, Ho Chi Minh",
            idCardNumber: idCard ?? $"ID{uniqueSuffix}").Value;
    }

    [Fact]
    public async Task Repository_Should_PersistDriver_AndRetrieveById()
    {
        var driver = BuildDriver();
        await fixture.DriverRepository.AddAsync(driver);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.DriverRepository.GetByIdAsync(driver.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(driver.Id);
        retrieved.Email.Should().Be(driver.Email);
        retrieved.VerificationStatus.Should().Be(DriverVerificationStatus.Draft);
    }

    [Fact]
    public async Task Repository_Should_FindDriver_ByLicenseNumber()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var licenseNumber = $"LIC{uniqueSuffix}";
        var driver = BuildDriver(license: licenseNumber);
        await fixture.DriverRepository.AddAsync(driver);
        await fixture.UnitOfWork.SaveChangesAsync();

        var retrieved = await fixture.DriverRepository.GetByLicenseNumberAsync(licenseNumber.ToUpperInvariant());

        retrieved.Should().NotBeNull();
        retrieved!.LicenseNumber.Should().Be(licenseNumber.ToUpperInvariant());
    }

    [Fact]
    public async Task Repository_Should_ReturnTrue_ForExistingIdCardNumber()
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var idCard = $"ID{uniqueSuffix}";
        var driver = BuildDriver(idCard: idCard);
        await fixture.DriverRepository.AddAsync(driver);
        await fixture.UnitOfWork.SaveChangesAsync();

        var exists = await fixture.DriverRepository.ExistsByIdCardNumberAsync(idCard);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Repository_Should_ReturnFalse_ForNonExistentIdCardNumber()
    {
        var nonExistentId = Guid.NewGuid().ToString("N");

        var exists = await fixture.DriverRepository.ExistsByIdCardNumberAsync(nonExistentId);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Repository_Should_ReturnNull_ForNonExistentDriver()
    {
        var result = await fixture.DriverRepository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
