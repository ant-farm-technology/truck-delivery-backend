namespace TruckDelivery.Payment.Application.Interfaces;

public interface IPaymentGateway
{
    /// <summary>
    /// Generates a redirect URL the customer must visit to complete payment.
    /// For COD this returns null (no redirect needed).
    /// </summary>
    Task<string?> CreatePaymentUrlAsync(Guid paymentId, decimal amount, string currency,
        string orderInfo, string clientIpAddress, CancellationToken ct = default);

    /// <summary>
    /// Verifies a gateway callback and returns (success, transactionRef).
    /// </summary>
    Task<(bool IsSuccess, string? TransactionRef, string? FailureReason)> VerifyCallbackAsync(
        IReadOnlyDictionary<string, string> queryParams, CancellationToken ct = default);
}
