using TruckDelivery.Shared.Contracts.Events;

namespace TruckDelivery.Shipment.Application.IntegrationEvents;

public sealed record DispatcherConfirmationRequiredEvent(
    Guid ShipmentId,
    Guid OrderId,
    string? BinCheckWarnings) : IntegrationEvent;
