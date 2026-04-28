using TruckDelivery.Driver.Domain.Events;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Domain;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Domain.Aggregates;

public sealed class Vehicle : AggregateRoot<Guid>
{
    private Vehicle() { }
    private Vehicle(Guid id) : base(id) { }

    public string LicensePlate { get; private set; } = default!;
    public string Brand { get; private set; } = default!;
    public string Model { get; private set; } = default!;
    public VehicleType Type { get; private set; }
    public decimal MaxWeightKg { get; private set; }
    public decimal MaxVolumeCbm { get; private set; }
    public int YearOfManufacture { get; private set; }
    public VehicleStatus Status { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Result<Vehicle> Create(
        string licensePlate,
        string brand,
        string model,
        VehicleType type,
        decimal maxWeightKg,
        decimal maxVolumeCbm,
        int yearOfManufacture)
    {
        if (string.IsNullOrWhiteSpace(licensePlate))
            return Result.Failure<Vehicle>(Error.Validation("Vehicle.LicensePlate", "License plate is required."));
        if (maxWeightKg <= 0)
            return Result.Failure<Vehicle>(Error.Validation("Vehicle.MaxWeightKg", "Max weight must be positive."));
        if (maxVolumeCbm <= 0)
            return Result.Failure<Vehicle>(Error.Validation("Vehicle.MaxVolumeCbm", "Max volume must be positive."));

        var vehicle = new Vehicle(Guid.NewGuid())
        {
            LicensePlate = licensePlate.ToUpperInvariant(),
            Brand = brand,
            Model = model,
            Type = type,
            MaxWeightKg = maxWeightKg,
            MaxVolumeCbm = maxVolumeCbm,
            YearOfManufacture = yearOfManufacture,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        vehicle.RaiseDomainEvent(new VehicleRegisteredDomainEvent(vehicle.Id, vehicle.LicensePlate, vehicle.Type));
        return Result.Success(vehicle);
    }

    public Result AssignDriver(Guid driverId)
    {
        if (Status == VehicleStatus.Maintenance)
            return Result.Failure(Error.Conflict("Vehicle", "Vehicle is under maintenance."));

        AssignedDriverId = driverId;
        Status = VehicleStatus.InUse;
        UpdatedAt = DateTime.UtcNow;

        RaiseDomainEvent(new VehicleAssignedToDriverDomainEvent(Id, driverId));
        return Result.Success();
    }

    public void UnassignDriver()
    {
        AssignedDriverId = null;
        Status = VehicleStatus.Available;
        UpdatedAt = DateTime.UtcNow;
    }

    public Result SetMaintenance()
    {
        if (AssignedDriverId.HasValue)
            return Result.Failure(Error.Conflict("Vehicle", "Cannot set maintenance while vehicle is assigned to a driver."));

        Status = VehicleStatus.Maintenance;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }
}
