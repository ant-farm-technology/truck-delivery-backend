using MediatR;
using Microsoft.Extensions.Logging;
using TruckDelivery.Analytics.Domain.Documents;
using TruckDelivery.Analytics.Domain.Repositories;

namespace TruckDelivery.Analytics.Application.Commands.RecordFraudAlert;

public sealed class RecordFraudAlertCommandHandler(
    IFraudAlertRepository repository,
    ILogger<RecordFraudAlertCommandHandler> logger)
    : IRequestHandler<RecordFraudAlertCommand>
{
    public async Task Handle(RecordFraudAlertCommand request, CancellationToken ct)
    {
        var alert = FraudAlert.Create(
            request.OriginalDriverId, request.ReplacementDriverId,
            request.SwapCount, request.DetectedAt);

        await repository.AddAsync(alert, ct);

        logger.LogWarning(
            "Fraud alert recorded: Driver={OriginalDriverId} ↔ Driver={ReplacementDriverId} Swaps={SwapCount}",
            request.OriginalDriverId, request.ReplacementDriverId, request.SwapCount);
    }
}
