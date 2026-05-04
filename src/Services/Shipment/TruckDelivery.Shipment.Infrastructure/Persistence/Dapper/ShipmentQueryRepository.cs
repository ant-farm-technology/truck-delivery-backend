using System.Text;
using Dapper;
using TruckDelivery.Shipment.Application.DTOs;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.Dapper;

public sealed class ShipmentQueryRepository(IDbConnectionFactory connectionFactory)
{
    private const string SelectColumns = """
        id             AS Id,
        order_id       AS OrderId,
        customer_id    AS CustomerId,
        status         AS Status,
        pickup_city    AS PickupCity,
        pickup_province AS PickupProvince,
        delivery_city  AS DeliveryCity,
        delivery_province AS DeliveryProvince,
        total_weight_kg AS TotalWeightKg,
        total_volume_cbm AS TotalVolumeCbm,
        assigned_driver_id AS AssignedDriverId,
        assigned_vehicle_id AS AssignedVehicleId,
        route_distance_m AS DistanceMeters,
        failure_reason AS FailureReason,
        created_at     AS CreatedAt,
        updated_at     AS UpdatedAt
        """;

    public async Task<ShipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await connectionFactory.CreateConnectionAsync(ct);
        var sql = $"SELECT {SelectColumns} FROM shipment.shipments WHERE id = @Id";
        return await conn.QueryFirstOrDefaultAsync<ShipmentDto>(sql, new { Id = id });
    }

    public async Task<PagedResult<ShipmentDto>> ListAsync(
        string? status, Guid? customerId, Guid? driverId, Guid? orderId,
        int page, int pageSize, CancellationToken ct = default)
    {
        using var conn = await connectionFactory.CreateConnectionAsync(ct);

        var where = new StringBuilder("WHERE 1=1");
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(status)) { where.Append(" AND status = @Status"); p.Add("Status", status); }
        if (customerId.HasValue) { where.Append(" AND customer_id = @CustomerId"); p.Add("CustomerId", customerId); }
        if (driverId.HasValue) { where.Append(" AND assigned_driver_id = @DriverId"); p.Add("DriverId", driverId); }
        if (orderId.HasValue) { where.Append(" AND order_id = @OrderId"); p.Add("OrderId", orderId); }

        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);

        var countSql = $"SELECT COUNT(*) FROM shipment.shipments {where}";
        var itemsSql = $"SELECT {SelectColumns} FROM shipment.shipments {where} ORDER BY created_at DESC LIMIT @PageSize OFFSET @Offset";

        var total = await conn.ExecuteScalarAsync<int>(countSql, p);
        var items = (await conn.QueryAsync<ShipmentDto>(itemsSql, p)).AsList();

        return new PagedResult<ShipmentDto>(items, total, page, pageSize);
    }
}
