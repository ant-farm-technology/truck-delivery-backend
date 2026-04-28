using MongoDB.Bson.Serialization.Attributes;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public sealed class ShipmentSagaState
{
    [BsonId]
    public Guid SagaId { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AssignedDriverId { get; set; }
    public Guid? AssignedVehicleId { get; set; }
    public ShipmentSagaStatus Status { get; set; }
    public List<string> CompletedSteps { get; set; } = [];
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public double? DistanceMeters { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int Version { get; set; }
}
