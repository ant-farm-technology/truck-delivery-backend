using MediatR;
using TruckDelivery.Identity.Application.Commands.Login;
using TruckDelivery.Identity.Application.Services;
using TruckDelivery.Identity.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Identity.Application.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(IUserRepository userRepository, IJwtService jwtService, IUnitOfWork unitOfWork) : IRequestHandler<RefreshTokenCommand, Result<LoginResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    public async Task<Result<LoginResult>> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct);
        if (user is null || !user.IsRefreshTokenValid(request.RefreshToken))
        {
            return Result.Failure<LoginResult>(Error.Unauthorized("Invalid or expired refresh token"));
        }

        var accessToken = jwtService.GenerateAccessToken(user);
        var newRefreshToken = jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.Add(RefreshTokenTtl);

        user.SetRefreshToken(newRefreshToken, expiresAt);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new LoginResult(accessToken, newRefreshToken, expiresAt));
    }
}
