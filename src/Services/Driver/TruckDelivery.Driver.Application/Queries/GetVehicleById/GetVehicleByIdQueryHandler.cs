using Dapper;
using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.GetVehicleById;

public sealed class GetVehicleByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<GetVehicleByIdQuery, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(GetVehicleByIdQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT Id, LicensePlate, Brand, Model, Type, MaxWeightKg, MaxVolumeCbm,
                   YearOfManufacture, Status, AssignedDriverId, CreatedAt
            FROM driver.vehicles
            WHERE Id = @VehicleId
            """;

        var dto = await conn.QuerySingleOrDefaultAsync<VehicleDto>(sql, new { request.VehicleId });

        return dto is null
            ? Result.Failure<VehicleDto>(Error.NotFound("Vehicle", request.VehicleId))
            : Result.Success(dto);
    }
}
