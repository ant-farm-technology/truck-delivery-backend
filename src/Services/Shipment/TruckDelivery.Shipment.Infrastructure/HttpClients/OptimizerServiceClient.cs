using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace TruckDelivery.Shipment.Infrastructure.HttpClients;

public sealed class OptimizerServiceClient(HttpClient httpClient, ILogger<OptimizerServiceClient> logger)
{
    public async Task<OptimizeResult?> OptimizeAsync(OptimizeRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/optimize", request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OptimizeResult>(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Optimizer call failed");
            return null;
        }
    }
}

public sealed record OptimizeRequest(
    IReadOnlyList<DriverInfo> Drivers,
    IReadOnlyList<OrderInfo> Orders,
    double[][] DistanceMatrix,
    int DepotIndex,
    int SolverTimeoutSeconds = 10);

public sealed record DriverInfo(
    Guid DriverId,
    Guid VehicleId,
    int LocationIndex,
    double MaxWeightKg,
    double MaxVolumeCbm);

public sealed record OrderInfo(
    Guid OrderId,
    int PickupIndex,
    int DeliveryIndex,
    double WeightKg,
    double VolumeCbm);

public sealed record OptimizeResult(
    IReadOnlyList<DriverAssignment> Assignments,
    IReadOnlyList<Guid> UnassignedOrderIds,
    bool Feasible,
    string StrategyUsed);

public sealed record DriverAssignment(Guid DriverId, Guid VehicleId, IReadOnlyList<Guid> OrderIds);
