using Dapper;
using MediatR;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Application.Queries.ListPendingVerification;

public sealed record ListPendingVerificationQuery(int Page = 1, int PageSize = 20)
    : IRequest<Result<IReadOnlyList<PendingVerificationDto>>>;

public sealed record PendingVerificationDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string LicenseNumber,
    string LicenseGrade,
    string VerificationStatus,
    float? OcrConfidenceScore,
    DateTime CreatedAt);

public sealed class ListPendingVerificationQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IRequestHandler<ListPendingVerificationQuery, Result<IReadOnlyList<PendingVerificationDto>>>
{
    public async Task<Result<IReadOnlyList<PendingVerificationDto>>> Handle(
        ListPendingVerificationQuery request, CancellationToken ct)
    {
        using var conn = await dbConnectionFactory.CreateConnectionAsync(ct);

        const string sql = """
            SELECT Id, Email, FirstName, LastName, PhoneNumber, LicenseNumber,
                   LicenseGrade, VerificationStatus, OcrConfidenceScore, CreatedAt
            FROM driver.drivers
            WHERE VerificationStatus IN (1, 3)
            ORDER BY CreatedAt ASC
            LIMIT @PageSize OFFSET @Offset
            """;

        var items = await conn.QueryAsync<PendingVerificationDto>(sql, new
        {
            Offset = (request.Page - 1) * request.PageSize,
            request.PageSize
        });

        return Result.Success<IReadOnlyList<PendingVerificationDto>>(items.AsList());
    }
}
