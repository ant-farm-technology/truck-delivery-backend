using System.Text.Json;
using MediatR;
using TruckDelivery.Driver.Application.IntegrationEvents;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;
using TruckDelivery.Shared.Infrastructure.Persistence.Outbox;

namespace TruckDelivery.Driver.Application.Commands.ApplyOcrResult;

public sealed class ApplyOcrResultCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork,
    IOutboxRepository outboxRepository)
    : IRequestHandler<ApplyOcrResultCommand, Result>
{
    public async Task<Result> Handle(ApplyOcrResultCommand request, CancellationToken ct)
    {
        var driver = await driverRepository.GetByIdAsync(request.DriverId, ct);
        if (driver is null)
            return Result.Failure(Error.NotFound("Driver", request.DriverId));

        var status = request.VerificationStatus switch
        {
            "ocr_verified" => DriverVerificationStatus.OcrVerified,
            "manual_review" => DriverVerificationStatus.ManualReview,
            "rejected" => DriverVerificationStatus.Rejected,
            _ => DriverVerificationStatus.ManualReview
        };

        driver.ApplyOcrResult(request.ConfidenceScore, status, request.Notes);
        driverRepository.Update(driver);

        if (status == DriverVerificationStatus.ManualReview)
        {
            var @event = new DriverManualReviewRequiredEvent
            {
                DriverId = driver.Id,
                DriverName = $"{driver.FirstName} {driver.LastName}",
                ConfidenceScore = request.ConfidenceScore,
                Notes = request.Notes
            };
            await outboxRepository.AddAsync(OutboxMessage.Create(
                eventType: nameof(DriverManualReviewRequiredEvent),
                topic: "driver.driver.manual-review-required",
                partitionKey: driver.Id.ToString(),
                payload: JsonSerializer.Serialize(@event)), ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
