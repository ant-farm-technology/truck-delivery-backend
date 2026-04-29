namespace TruckDelivery.Shipment.Application.Interfaces;

public interface IBinCheckService
{
    Task<BinCheckServiceResult?> CheckAsync(BinCheckServiceRequest request, CancellationToken ct = default);
}

public sealed record BinCheckServiceRequest(
    double TruckLengthM,
    double TruckWidthM,
    double TruckHeightM,
    double TruckMaxWeightKg,
    IReadOnlyList<BinCheckPackage> Packages);

public sealed record BinCheckPackage(
    Guid PackageId,
    double LengthM,
    double WidthM,
    double HeightM,
    double WeightKg,
    int DeliveryRank,
    bool CanTilt = false,
    double Value = 0);

public sealed record BinCheckServiceResult(
    bool AllFit,
    bool RequiresTilt,
    bool RequiresDispatcherConfirmation,
    IReadOnlyList<string> AccessibilityWarnings);
