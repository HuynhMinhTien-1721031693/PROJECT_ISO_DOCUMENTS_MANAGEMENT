using FluentValidation;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Application.Common.Interfaces;
using MediatR;

namespace IsoDoc.Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(int Page = 1, int PageSize = 20, bool UnreadOnly = false)
    : IRequest<Result<PagedList<UserNotificationDto>>>;

public sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, Result<PagedList<UserNotificationDto>>>
{
    private readonly IUserNotificationRepository _notifications;
    private readonly ICurrentUserService _currentUser;

    public GetNotificationsQueryHandler(
        IUserNotificationRepository notifications,
        ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedList<UserNotificationDto>>> Handle(
        GetNotificationsQuery query,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Result<PagedList<UserNotificationDto>>.Failure("Chua dang nhap.", "UNAUTHORIZED");

        var (items, total) = await _notifications.GetPageForUserAsync(
            userId.Value, query.Page, query.PageSize, query.UnreadOnly, ct);

        var dtos = items
            .Select(n => new UserNotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                ActionUrl = n.ActionUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToList();

        return Result<PagedList<UserNotificationDto>>.Success(
            new PagedList<UserNotificationDto>(dtos, total, query.Page, query.PageSize));
    }
}
