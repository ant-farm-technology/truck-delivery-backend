using MongoDB.Bson.Serialization.Attributes;

namespace TruckDelivery.Shipment.Infrastructure.Persistence.Mongo;

public sealed class BreakdownSagaState
{
    [BsonId]
    public Guid SagaId { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid OriginalDriverId { get; set; }
    public Guid? OriginalVehicleId { get; set; }
    public Guid? ReplacementDriverId { get; set; }
    public double BreakdownLatitude { get; set; }
    public double BreakdownLongitude { get; set; }
    public string FraudRiskLevel { get; set; } = "Low";
    public BreakdownSagaStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public int Version { get; set; }
}

public enum BreakdownSagaStatus
{
    Started,
    ReassignmentRequested,
    Completed,
    Failed
}
