using TruckDelivery.Payment.Application.Interfaces;

namespace TruckDelivery.Payment.Infrastructure.Gateways;

public sealed class CodGateway : IPaymentGateway
{
    // COD requires no redirect — payment is collected on delivery
    public Task<string?> CreatePaymentUrlAsync(Guid paymentId, decimal amount, string currency,
        string orderInfo, string clientIpAddress, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<(bool IsSuccess, string? TransactionRef, string? FailureReason)> VerifyCallbackAsync(
        IReadOnlyDictionary<string, string> queryParams, CancellationToken ct = default)
        => Task.FromResult<(bool, string?, string?)>((true, null, null));
}
