using MediatR;
using TruckDelivery.Identity.Application.Commands.Login;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Identity.Application.Commands.RefreshToken;

public sealed record RefreshTokenCommand(Guid UserId, string RefreshToken) : IRequest<Result<LoginResult>>;
