namespace TruckDelivery.Driver.Application.DTOs;

public sealed record DriverDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string LicenseNumber,
    string Status,
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
    int YearOfManufacture,
    string Status,
    Guid? AssignedDriverId,
    DateTime CreatedAt);
