using IsoDoc.Application.Common.Models;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Notifications.Commands.MarkAllNotificationsRead;

public sealed record MarkAllNotificationsReadCommand : IRequest<Result>;

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, Result>
{
    private readonly IUserNotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public MarkAllNotificationsReadCommandHandler(
        IUserNotificationRepository notifications,
        ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(MarkAllNotificationsReadCommand command, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Result.Failure("Chua dang nhap.", "UNAUTHORIZED");

        await _notifications.MarkAllReadAsync(userId.Value, ct);
        return Result.Success();
    }
}
