namespace TruckDelivery.Shipment.Application.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
