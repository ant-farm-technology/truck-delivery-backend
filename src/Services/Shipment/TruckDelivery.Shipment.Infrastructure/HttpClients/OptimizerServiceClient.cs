using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TruckDelivery.Shipment.Application.Interfaces;

namespace TruckDelivery.Shipment.Infrastructure.HttpClients;

public sealed class OptimizerServiceClient(HttpClient httpClient, ILogger<OptimizerServiceClient> logger)
    : IBinCheckService
{
    // Python Optimizer uses snake_case JSON — must match on both send and receive.
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<OptimizeResult?> OptimizeAsync(OptimizeRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/optimize", request, SnakeCaseOptions, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<OptimizeResult>(SnakeCaseOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Optimizer call failed");
            return null;
        }
    }

    public async Task<BinCheckServiceResult?> CheckAsync(BinCheckServiceRequest request, CancellationToken ct = default)
    {
        try
        {
            var payload = new BinCheckRequest(
                new BinTruckInfo(request.TruckLengthM, request.TruckWidthM, request.TruckHeightM, request.TruckMaxWeightKg),
                request.Packages.Select(p => new BinPackageInfo(p.PackageId, p.LengthM, p.WidthM, p.HeightM, p.WeightKg, p.DeliveryRank, p.CanTilt, p.Value)).ToList());

            var response = await httpClient.PostAsJsonAsync("/bin-check", payload, SnakeCaseOptions, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<BinCheckResult>(SnakeCaseOptions, ct);
            return result is null ? null : new BinCheckServiceResult(result.AllFit, result.RequiresTilt, result.RequiresDispatcherConfirmation, result.AccessibilityWarnings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BinCheck call failed");
            return null;
        }
    }
}

public sealed record OptimizeRequest(
    IReadOnlyList<DriverInfo> Drivers,
    IReadOnlyList<OrderInfo> Orders,
    double[][] DistanceMatrix,
    int DepotIndex,
    int SolverTimeoutSeconds = 10,
    IReadOnlyList<string>? RequiredLicenseGrades = null);

public sealed record DriverInfo(
    Guid DriverId,
    Guid VehicleId,
    int LocationIndex,
    double MaxWeightKg,
    double MaxVolumeCbm,
    string? LicenseGrade = null);

public sealed record OrderInfo(
    Guid OrderId,
    int PickupIndex,
    int DeliveryIndex,
    double WeightKg,
    double VolumeCbm,
    long? EarliestPickupUnix = null,
    long? HardDeadlineUnix = null,
    long? DesiredDeliveryUnix = null,
    string? SlaTier = null);

public sealed record OptimizeResult(
    IReadOnlyList<DriverAssignment> Assignments,
    IReadOnlyList<Guid> UnassignedOrderIds,
    bool Feasible,
    string StrategyUsed);

public sealed record DriverAssignment(Guid DriverId, Guid VehicleId, IReadOnlyList<Guid> OrderIds);

public sealed record BinCheckRequest(BinTruckInfo Truck, IReadOnlyList<BinPackageInfo> Packages);

public sealed record BinTruckInfo(double LengthM, double WidthM, double HeightM, double MaxWeightKg);

public sealed record BinPackageInfo(
    Guid PackageId,
    double LengthM,
    double WidthM,
    double HeightM,
    double WeightKg,
    int DeliveryRank,
    bool CanTilt = false,
    double Value = 0);

public sealed record BinCheckResult(
    bool AllFit,
    bool RequiresTilt,
    bool RequiresDispatcherConfirmation,
    IReadOnlyList<string> AccessibilityWarnings);
