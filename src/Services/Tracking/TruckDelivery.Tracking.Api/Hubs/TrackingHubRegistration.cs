using TruckDelivery.Tracking.Infrastructure.Hubs;

namespace TruckDelivery.Tracking.Api.Hubs;

// Map hub endpoint — called from Program.cs
public static class TrackingHubRegistration
{
    public static void MapTrackingHub(this WebApplication app)
        => app.MapHub<TrackingHub>("/hubs/tracking");
}
