using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.AdminVerifyDriver;

public sealed record AdminVerifyDriverCommand(Guid DriverId, string? Notes = null) : IRequest<Result>;
