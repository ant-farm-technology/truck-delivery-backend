using MediatR;
using Microsoft.EntityFrameworkCore;
using TruckDelivery.Notification.Domain.Aggregates;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Notification.Application.Commands.RegisterDevice;

// Upsert: one token per user+platform pair — replaces previous token on re-register
public sealed class RegisterDeviceCommandHandler(IDeviceTokenStore store) : IRequestHandler<RegisterDeviceCommand, Result>
{
    public async Task<Result> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        await store.UpsertAsync(request.UserId, request.Token, request.Platform, ct);
        return Result.Success();
    }
}

public interface IDeviceTokenStore
{
    Task UpsertAsync(Guid userId, string token, string platform, CancellationToken ct = default);
}
