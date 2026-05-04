using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.AdminRejectDriver;

public sealed record AdminRejectDriverCommand(Guid DriverId, string Reason) : IRequest<Result>;
