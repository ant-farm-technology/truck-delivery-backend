using MediatR;
using TruckDelivery.Notification.Domain.Aggregates;
using TruckDelivery.Shared.Common.Primitives;

namespace TruckDelivery.Notification.Application.Commands.SendNotification;

public sealed record SendNotificationCommand(
    Guid RecipientId,
    NotificationType Type,
    IReadOnlyList<NotificationChannel> Channels,
    string Title,
    string Body,
    string? RecipientPhone = null,
    string? RecipientEmail = null
) : IRequest<Result<Guid>>;
