using IsoDoc.Application.Notifications.Commands.MarkAllNotificationsRead;
using IsoDoc.Application.Notifications.Commands.MarkNotificationRead;
using IsoDoc.Application.Notifications.Queries.GetNotifications;
using IsoDoc.Application.Notifications.Queries.GetUnreadNotificationCount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetNotificationsQuery(page, pageSize, unreadOnly), ct);
        return result.IsSuccess ? PagedResult(result.Value!) : FromResult(result);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetUnreadNotificationCountQuery(), ct);
        return result.IsSuccess ? OkResult(result.Value) : FromResult(result);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new MarkNotificationReadCommand(id), ct);
        return FromResult(result);
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return FromResult(result);
    }
}
