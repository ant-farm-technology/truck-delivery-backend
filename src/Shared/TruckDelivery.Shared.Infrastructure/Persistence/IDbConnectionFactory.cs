using System.Data;

namespace TruckDelivery.Shared.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
