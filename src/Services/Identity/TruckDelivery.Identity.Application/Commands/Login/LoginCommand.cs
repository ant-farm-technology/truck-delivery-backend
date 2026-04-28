using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Identity.Application.Commands.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResult>>;
