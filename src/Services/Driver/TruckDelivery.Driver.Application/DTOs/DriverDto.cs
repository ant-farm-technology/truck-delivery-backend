namespace TruckDelivery.Driver.Application.DTOs;

public sealed record DriverDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string LicenseNumber,
    string Status,
    string VerificationStatus,
    string LicenseGrade,
    int TrustScore,
    Guid? CurrentVehicleId,
    bool IsActive,
    DateTime CreatedAt);

public sealed record DriverSummaryDto(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Status,
    Guid? CurrentVehicleId);

public sealed record VehicleDto(
    Guid Id,
    string LicensePlate,
    string Brand,
    string Model,
    string Type,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    decimal LengthM,
    decimal WidthM,
    decimal HeightM,
    int YearOfManufacture,
    string RegistrationNumber,
    DateOnly RegistrationExpiryDate,
    string Status,
    Guid? AssignedDriverId,
    DateTime CreatedAt);
