using MediatR;
using TruckDelivery.Identity.Application.Services;
using TruckDelivery.Identity.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Common.Persistence;

namespace TruckDelivery.Identity.Application.Commands.Login;

public sealed class LoginCommandHandler(IUserRepository userRepository, IJwtService jwtService, IUnitOfWork unitOfWork) : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);
        if (user is null || !user.VerifyPassword(request.Password))
        {
            return Result.Failure<LoginResult>(Error.Unauthorized("Invalid email or password"));
        }

        if (!user.IsActive)
        {
            return Result.Failure<LoginResult>(Error.Unauthorized("Account is disabled"));
        }

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.Add(RefreshTokenTtl);

        user.SetRefreshToken(refreshToken, expiresAt);
        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new LoginResult(accessToken, refreshToken, expiresAt));
    }
}
