using MediatR;
using Microsoft.Extensions.Logging;
using TruckDelivery.Notification.Application.Interfaces;
using TruckDelivery.Notification.Domain.Aggregates;
using TruckDelivery.Notification.Domain.Repositories;
using TruckDelivery.Shared.Common.Persistence;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Notification.Application.Commands.SendNotification;

public sealed class SendNotificationCommandHandler(
    INotificationRepository repository,
    IUnitOfWork unitOfWork,
    IPushNotificationSender pushSender,
    ISmsNotificationSender smsSender,
    IEmailNotificationSender emailSender,
    ILogger<SendNotificationCommandHandler> logger)
    : IRequestHandler<SendNotificationCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SendNotificationCommand cmd, CancellationToken ct)
    {
        Guid firstId = Guid.Empty;

        foreach (var channel in cmd.Channels)
        {
            var notification = NotificationRecord.Create(cmd.RecipientId, cmd.Type, channel, cmd.Title, cmd.Body);

            try
            {
                switch (channel)
                {
                    case NotificationChannel.Push:
                        await pushSender.SendAsync(cmd.RecipientId, cmd.Title, cmd.Body, ct);
                        break;
                    case NotificationChannel.Sms when cmd.RecipientPhone is not null:
                        await smsSender.SendAsync(cmd.RecipientPhone, cmd.Body, ct);
                        break;
                    case NotificationChannel.Email when cmd.RecipientEmail is not null:
                        await emailSender.SendAsync(cmd.RecipientEmail, cmd.Title, cmd.Body, ct);
                        break;
                }

                notification.MarkSent();
                logger.LogInformation("Sent {Type} via {Channel} to RecipientId={RecipientId}",
                    cmd.Type, channel, cmd.RecipientId);
            }
            catch (Exception ex)
            {
                notification.MarkFailed(ex.Message);
                logger.LogError(ex, "Failed to send {Type} via {Channel} to RecipientId={RecipientId}",
                    cmd.Type, channel, cmd.RecipientId);
            }

            await repository.AddAsync(notification, ct);
            if (firstId == Guid.Empty) firstId = notification.Id;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(firstId);
    }
}
