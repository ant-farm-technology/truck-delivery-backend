using Dapper;
using TruckDelivery.Shipment.Application.DTOs;
using TruckDelivery.Shared.Infrastructure.Persistence;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.Dapper;

public sealed class ShipmentQueryRepository(IDbConnectionFactory connectionFactory)
{
    public async Task<ShipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT
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
            FROM shipment.shipments
            WHERE id = @Id
            """;
        return await conn.QueryFirstOrDefaultAsync<ShipmentDto>(sql, new { Id = id });
    }
}
