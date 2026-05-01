using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using TruckDelivery.Notification.Application.Interfaces;

namespace TruckDelivery.Notification.Infrastructure.Notifications;

public sealed class TwilioSmsSender : ISmsNotificationSender
{
    private readonly string _fromNumber;
    private readonly ILogger<TwilioSmsSender> _logger;

    public TwilioSmsSender(IConfiguration configuration, ILogger<TwilioSmsSender> logger)
    {
        _logger = logger;
        var accountSid = configuration["Twilio:AccountSid"]
            ?? throw new InvalidOperationException("Twilio:AccountSid not configured");
        var authToken = configuration["Twilio:AuthToken"]
            ?? throw new InvalidOperationException("Twilio:AuthToken not configured");
        _fromNumber = configuration["Twilio:FromNumber"]
            ?? throw new InvalidOperationException("Twilio:FromNumber not configured");

        TwilioClient.Init(accountSid, authToken);
    }

    public async Task SendAsync(string phone, string body, CancellationToken ct = default)
    {
        var message = await MessageResource.CreateAsync(
            to: new Twilio.Types.PhoneNumber(phone),
            from: new Twilio.Types.PhoneNumber(_fromNumber),
            body: body);

        _logger.LogInformation("SMS sent via Twilio Sid={Sid} To={Phone}", message.Sid, phone);
    }
}
