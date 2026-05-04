namespace TruckDelivery.Payment.Application.DTOs;

public sealed record EscrowDto(
    Guid Id,
    Guid ShipmentId,
    Guid OrderId,
    Guid OriginalDriverId,
    Guid ReplacementDriverId,
    decimal LockedAmount,
    string Currency,
    string Status,
    string? ResolutionNote,
    DateTime LockedAt,
    DateTime? ResolvedAt);
