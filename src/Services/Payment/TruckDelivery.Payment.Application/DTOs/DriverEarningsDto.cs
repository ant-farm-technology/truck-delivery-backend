namespace TruckDelivery.Payment.Application.DTOs;

public sealed record DriverEarningsDto(
    Guid DriverId,
    decimal TotalEarned,
    int DeliveryCount,
    IReadOnlyList<EarningItemDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record EarningItemDto(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt);
