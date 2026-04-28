using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace TruckDelivery.Shipment.Infrastructure.HttpClients;

public sealed class RouteServiceClient(HttpClient httpClient, ILogger<RouteServiceClient> logger)
{
    public async Task<RouteResult?> GetRouteAsync(
        double pickupLat, double pickupLng,
        double deliveryLat, double deliveryLng,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/route?from_lat={pickupLat}&from_lng={pickupLng}&to_lat={deliveryLat}&to_lng={deliveryLng}";
            return await httpClient.GetFromJsonAsync<RouteResult>(url, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RouteService call failed");
            return null;
        }
    }

    public async Task<MatrixResult?> GetDistanceMatrixAsync(
        IReadOnlyList<(double Lat, double Lng)> locations,
        CancellationToken ct = default)
    {
        try
        {
            var points = locations.Select(l => $"{l.Lat},{l.Lng}");
            var url = $"/matrix?points={string.Join("|", points)}";
            return await httpClient.GetFromJsonAsync<MatrixResult>(url, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RouteService matrix call failed");
            return null;
        }
    }
}

public sealed record RouteResult(double DistanceMeters, double DurationSeconds, string? EncodedPolyline);
public sealed record MatrixResult(double[][] Distances, double[][] Durations);
