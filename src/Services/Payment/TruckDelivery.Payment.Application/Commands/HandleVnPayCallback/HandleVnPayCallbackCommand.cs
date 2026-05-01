using MediatR;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Payment.Application.Commands.HandleVnPayCallback;

public sealed record HandleVnPayCallbackCommand(
    IReadOnlyDictionary<string, string> QueryParams) : IRequest<Result<HandleVnPayCallbackResult>>;

public sealed record HandleVnPayCallbackResult(bool IsSuccess, Guid PaymentId, string? FailureReason);
