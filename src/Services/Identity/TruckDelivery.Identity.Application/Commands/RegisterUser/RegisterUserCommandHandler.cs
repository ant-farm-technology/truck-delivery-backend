using MediatR;
using TruckDelivery.Identity.Domain.Aggregates;
using TruckDelivery.Identity.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Contracts.Events;
using TruckDelivery.Shared.Infrastructure.Messaging;

namespace TruckDelivery.Identity.Application.Commands.RegisterUser;

public sealed class RegisterUserCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork, IEventBus eventBus) : IRequestHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    public async Task<Result<RegisterUserResult>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, ct))
        {
            return Result.Failure<RegisterUserResult>(Error.Conflict("User.Email", "Email already registered"));
        }

        var userResult = User.Create(request.Email, request.Password, request.FirstName, request.LastName, request.Role);
        if (userResult.IsFailure)
        {
            return Result.Failure<RegisterUserResult>(userResult.Error);
        }

        var user = userResult.Value;
        await userRepository.AddAsync(user, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await eventBus.PublishAsync(new UserRegisteredEvent(user.Id, user.Email, user.FirstName, user.LastName, user.Role.ToString()), ct);

        return Result.Success(new RegisterUserResult(user.Id, user.Email));
    }
}
