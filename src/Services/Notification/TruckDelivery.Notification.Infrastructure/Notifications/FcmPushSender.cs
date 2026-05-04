using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TruckDelivery.Notification.Application.Commands.RegisterDevice;
using TruckDelivery.Notification.Application.Interfaces;

namespace TruckDelivery.Notification.Infrastructure.Notifications;

public sealed class FcmPushSender : IPushNotificationSender
{
    private readonly IDeviceTokenStore _deviceTokenStore;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly bool _isConfigured;

    public FcmPushSender(
        IDeviceTokenStore deviceTokenStore,
        IConfiguration configuration,
        ILogger<FcmPushSender> logger)
    {
        _deviceTokenStore = deviceTokenStore;
        _logger = logger;

        var credentialsJson = configuration["Firebase:CredentialsJson"];
        if (!string.IsNullOrWhiteSpace(credentialsJson))
        {
            try
            {
                if (FirebaseApp.DefaultInstance is null)
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromJson(credentialsJson)
                    });
                }
                _isConfigured = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Firebase initialization failed — FCM push disabled");
            }
        }
        else
        {
            logger.LogInformation("Firebase:CredentialsJson not configured — FCM push disabled");
        }
    }

    public async Task SendAsync(Guid recipientId, string title, string body, CancellationToken ct = default)
    {
        if (!_isConfigured)
        {
            _logger.LogDebug("FCM not configured, skipping push to {RecipientId}", recipientId);
            return;
        }

        var tokens = await _deviceTokenStore.GetTokensByUserIdAsync(recipientId, ct);
        if (tokens.Count == 0)
        {
            _logger.LogDebug("No device tokens registered for {RecipientId}", recipientId);
            return;
        }

        var messages = tokens.Select(token => new Message
        {
            Token = token,
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body }
        }).ToList();

        var response = await FirebaseMessaging.DefaultInstance.SendEachAsync(messages, ct);

        var failed = response.Responses.Count(r => !r.IsSuccess);
        if (failed > 0)
            _logger.LogWarning("FCM: {Failed}/{Total} messages failed for RecipientId={RecipientId}",
                failed, messages.Count, recipientId);
        else
            _logger.LogInformation("FCM: sent {Count} push message(s) to RecipientId={RecipientId}",
                messages.Count, recipientId);
    }
}
