using System.Data;
using MySqlConnector;

namespace TruckDelivery.Shared.Infrastructure.Persistence.MySql;

public sealed class MySqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
