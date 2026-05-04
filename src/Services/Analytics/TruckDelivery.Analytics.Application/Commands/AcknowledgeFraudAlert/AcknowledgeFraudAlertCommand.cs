using MediatR;
using TruckDelivery.Analytics.Domain.Repositories;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Analytics.Application.Commands.AcknowledgeFraudAlert;

public sealed record AcknowledgeFraudAlertCommand(Guid AlertId) : IRequest<Result>;

public sealed class AcknowledgeFraudAlertCommandHandler(IFraudAlertRepository fraudAlertRepository)
    : IRequestHandler<AcknowledgeFraudAlertCommand, Result>
{
    public async Task<Result> Handle(AcknowledgeFraudAlertCommand request, CancellationToken ct)
    {
        var alert = await fraudAlertRepository.GetByIdAsync(request.AlertId, ct);
        if (alert is null)
            return Result.Failure(Error.NotFound("FraudAlert", request.AlertId));

        if (alert.IsAcknowledged)
            return Result.Success();

        alert.IsAcknowledged = true;
        await fraudAlertRepository.UpdateAsync(alert, ct);
        return Result.Success();
    }
}
