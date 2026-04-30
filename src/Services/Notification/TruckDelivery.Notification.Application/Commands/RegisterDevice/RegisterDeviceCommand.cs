using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Notification.Application.Commands.RegisterDevice;

public sealed record RegisterDeviceCommand(Guid UserId, string Token, string Platform) : IRequest<Result>;
