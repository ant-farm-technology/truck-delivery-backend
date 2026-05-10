using MediatR;
using TruckDelivery.Driver.Application.DTOs;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Queries.GetMyVehicle;

/// <summary>
/// Returns the vehicle currently assigned to the authenticated driver.
/// Resolves the vehicle via the driver's JWT sub → driver record → currentVehicleId.
/// </summary>
public sealed record GetMyVehicleQuery(Guid UserId) : IRequest<Result<VehicleDto>>;
