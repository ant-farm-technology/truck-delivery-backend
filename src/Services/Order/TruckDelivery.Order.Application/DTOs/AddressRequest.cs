namespace TruckDelivery.Order.Application.DTOs;

public sealed record AddressRequest(
    string Street,
    string City,
    string Province,
    string PostalCode,
    string Country = "VN");
