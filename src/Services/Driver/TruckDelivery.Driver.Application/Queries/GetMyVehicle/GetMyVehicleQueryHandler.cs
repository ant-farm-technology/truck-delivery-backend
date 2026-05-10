using Dapper;
using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.GetMyVehicle;

public sealed class GetMyVehicleQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<GetMyVehicleQuery, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(GetMyVehicleQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        // Step 1: resolve currentVehicleId from driver record (Driver.Id == UserId by design)
        const string vehicleIdSql = """
            SELECT CurrentVehicleId
            FROM driver.drivers
            WHERE Id = @UserId AND CurrentVehicleId IS NOT NULL
            """;

        var vehicleId = await conn.QuerySingleOrDefaultAsync<Guid?>(vehicleIdSql, new { request.UserId });

        if (!vehicleId.HasValue)
            return Result.Failure<VehicleDto>(Error.NotFound("Vehicle", "No vehicle currently assigned to this driver."));

        // Step 2: fetch full vehicle record
        const string vehicleSql = """
            SELECT Id, LicensePlate, Brand, Model, Type, MaxWeightKg, MaxVolumeCbm,
                   LengthM, WidthM, HeightM,
                   YearOfManufacture, RegistrationNumber, RegistrationExpiryDate,
                   Status, AssignedDriverId, CreatedAt
            FROM driver.vehicles
            WHERE Id = @VehicleId
            """;

        var dto = await conn.QuerySingleOrDefaultAsync<VehicleDto>(vehicleSql, new { VehicleId = vehicleId.Value });

        return dto is null
            ? Result.Failure<VehicleDto>(Error.NotFound("Vehicle", vehicleId.Value))
            : Result.Success(dto);
    }
}
