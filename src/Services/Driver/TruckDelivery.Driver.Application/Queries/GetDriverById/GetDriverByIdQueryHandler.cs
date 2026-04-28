using Dapper;
using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.GetDriverById;

public sealed class GetDriverByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<GetDriverByIdQuery, Result<DriverDto>>
{
    public async Task<Result<DriverDto>> Handle(GetDriverByIdQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT Id, Email, FirstName, LastName, PhoneNumber, LicenseNumber,
                   Status, CurrentVehicleId, IsActive, CreatedAt
            FROM driver.drivers
            WHERE Id = @DriverId
            """;

        var dto = await conn.QuerySingleOrDefaultAsync<DriverDto>(sql, new { request.DriverId });

        return dto is null
            ? Result.Failure<DriverDto>(Error.NotFound("Driver", request.DriverId))
            : Result.Success(dto);
    }
}
