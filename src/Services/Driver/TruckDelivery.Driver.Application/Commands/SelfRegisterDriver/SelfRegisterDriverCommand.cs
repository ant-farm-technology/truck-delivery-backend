using MediatR;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.SelfRegisterDriver;

public sealed record SelfRegisterDriverCommand(
    // From JWT — driverId == userId (set by Identity service)
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    // Personal info
    string IdCardNumber,
    DateOnly DateOfBirth,
    string Address,
    // License
    string LicenseNumber,
    LicenseGrade LicenseGrade,
    DateOnly LicenseExpiryDate,
    // Document photo URLs (uploaded to MinIO in Step 2)
    string PortraitPhotoUrl,
    string IdCardFrontUrl,
    string IdCardBackUrl,
    string LicenseFrontUrl,
    string LicenseBackUrl,
    string VehicleRegFrontUrl,
    string VehicleRegBackUrl,
    // Vehicle
    string LicensePlate,
    string Brand,
    string Model,
    VehicleType VehicleType,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    decimal LengthM,
    decimal WidthM,
    decimal HeightM,
    int YearOfManufacture,
    string RegistrationNumber,
    DateOnly RegistrationExpiryDate) : IRequest<Result<SelfRegisterDriverResult>>;

public sealed record SelfRegisterDriverResult(Guid DriverId, Guid VehicleId, string VerificationStatus);
