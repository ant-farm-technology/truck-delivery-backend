namespace TruckDelivery.Shipment.Domain.Exceptions;

public sealed class ShipmentDomainException(string message) : Exception(message);
