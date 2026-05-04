using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Identity.Application.Commands.RevokeRefreshToken;

public sealed record RevokeRefreshTokenCommand(Guid UserId) : IRequest<Result>;
