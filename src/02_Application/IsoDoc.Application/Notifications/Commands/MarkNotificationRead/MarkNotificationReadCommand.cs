using FluentValidation;
using IsoDoc.Application.Common.Models;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result>;

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.NotificationId).NotEmpty();
    }
}

public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Result>
{
    private readonly IUserNotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public MarkNotificationReadCommandHandler(
        IUserNotificationRepository notifications,
        ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(MarkNotificationReadCommand command, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Result.Failure("Chua dang nhap.", "UNAUTHORIZED");

        var n = await _notifications.GetByIdForUserAsync(command.NotificationId, userId.Value, ct);
        if (n is null)
            return Result.Failure("Khong tim thay thong bao.", "NOT_FOUND");

        await _notifications.MarkReadAsync(command.NotificationId, userId.Value, ct);
        return Result.Success();
    }
}
