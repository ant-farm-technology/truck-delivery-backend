using MediatR;
using TruckDelivery.Driver.Domain.Repositories;
using TruckDelivery.Driver.Domain.ValueObjects;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Driver.Application.Commands.ApplyOcrResult;

public sealed class ApplyOcrResultCommandHandler(
    IDriverRepository driverRepository,
    IUnitOfWork unitOfWork)
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
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }
}
