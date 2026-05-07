using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TruckDelivery.Payment.Application.Interfaces;

namespace TruckDelivery.Payment.Infrastructure.Gateways;

public sealed class VnPayGateway(IConfiguration configuration, ILogger<VnPayGateway> logger) : IPaymentGateway
{
    private readonly string _tmnCode = configuration["VnPay:TmnCode"] ?? throw new InvalidOperationException("VnPay:TmnCode not configured");
    private readonly string _hashSecret = configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("VnPay:HashSecret not configured");
    private readonly string _paymentUrl = configuration["VnPay:PaymentUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    private readonly string _returnUrl = configuration["VnPay:ReturnUrl"] ?? throw new InvalidOperationException("VnPay:ReturnUrl not configured");

    public Task<string?> CreatePaymentUrlAsync(Guid paymentId, decimal amount, string currency,
        string orderInfo, string clientIpAddress, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow.AddHours(7); // VNPay uses ICT (UTC+7)
        var createDate = now.ToString("yyyyMMddHHmmss");
        var expireDate = now.AddMinutes(15).ToString("yyyyMMddHHmmss");

        // VNPay amount is in smallest unit (VND × 100)
        var vnpAmount = ((long)(amount * 100)).ToString();

        var rawData = new SortedDictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _tmnCode,
            ["vnp_Amount"] = vnpAmount,
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = clientIpAddress,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _returnUrl,
            ["vnp_ExpireDate"] = expireDate,
            ["vnp_TxnRef"] = paymentId.ToString(), // use paymentId as transaction ref
        };

        var queryString = string.Join("&", rawData.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        var signData = string.Join("&", rawData.Select(kv => $"{kv.Key}={kv.Value}"));
        var signature = HmacSha512(_hashSecret, signData);

        var url = $"{_paymentUrl}?{queryString}&vnp_SecureHash={signature}";
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("VNPay URL created for paymentId={PaymentId}", paymentId);
        }
        return Task.FromResult<string?>(url);
    }

    public Task<(bool IsSuccess, string? TransactionRef, string? FailureReason)> VerifyCallbackAsync(
        IReadOnlyDictionary<string, string> queryParams, CancellationToken ct = default)
    {
        if (!queryParams.TryGetValue("vnp_SecureHash", out var receivedHash))
            return Task.FromResult<(bool, string?, string?)>((false, null, "Missing vnp_SecureHash"));

        var data = queryParams
            .Where(kv => kv.Key.StartsWith("vnp_") && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType")
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}");

        var signData = string.Join("&", data);
        var expectedHash = HmacSha512(_hashSecret, signData);

        if (!string.Equals(expectedHash, receivedHash))
        {
            logger.LogWarning("VNPay signature mismatch");
            return Task.FromResult<(bool, string?, string?)>((false, null, "Signature verification failed"));
        }

        queryParams.TryGetValue("vnp_TxnRef", out var txnRef);
        queryParams.TryGetValue("vnp_ResponseCode", out var responseCode);

        var isSuccess = responseCode == "00";
        var failureReason = isSuccess ? null : $"VNPay response code: {responseCode}";
        return Task.FromResult<(bool, string?, string?)>((isSuccess, txnRef, failureReason));
    }

    private static string HmacSha512(string key, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLower();
    }
}
