using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

public sealed record BreakdownReassignmentCompletedEvent : IntegrationEvent
{
    public Guid ShipmentId { get; init; }
    public Guid OrderId { get; init; }
    public Guid OriginalDriverId { get; init; }
    public Guid ReplacementDriverId { get; init; }
    public Guid ReplacementVehicleId { get; init; }
}
