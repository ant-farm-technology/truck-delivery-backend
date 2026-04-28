namespace TruckDelivery.Payment.Application.DTOs;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Status,
    string? FailureReason,
    DateTime CreatedAt);
