using Microsoft.EntityFrameworkCore;
using TruckDelivery.Driver.Domain.Aggregates;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Infrastructure.Persistence;

namespace TruckDelivery.Driver.Infrastructure.Repositories;

public sealed class BreakdownReportRepository(DriverDbContext context) : IBreakdownReportRepository
{
    public async Task AddAsync(BreakdownReport report, CancellationToken ct = default)
        => await context.BreakdownReports.AddAsync(report, ct);

    public async Task<IReadOnlyList<BreakdownReport>> GetByDriverIdAsync(Guid driverId, CancellationToken ct = default)
        => await context.BreakdownReports
            .Where(r => r.DriverId == driverId)
            .OrderByDescending(r => r.ReportedAt)
            .ToListAsync(ct);
}
