using IsoDoc.Application.Common.Models;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Notifications.Queries.GetUnreadNotificationCount;

public sealed record GetUnreadNotificationCountQuery : IRequest<Result<int>>;

public sealed class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IUserNotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public GetUnreadNotificationCountQueryHandler(
        IUserNotificationRepository notifications,
        ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Result<int>.Failure("Chua dang nhap.", "UNAUTHORIZED");

        var count = await _notifications.GetUnreadCountAsync(userId.Value, ct);
        return Result<int>.Success(count);
    }
}
