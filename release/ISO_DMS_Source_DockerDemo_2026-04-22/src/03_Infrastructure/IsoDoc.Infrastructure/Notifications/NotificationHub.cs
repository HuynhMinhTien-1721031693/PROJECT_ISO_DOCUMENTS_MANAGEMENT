using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Infrastructure.Notifications;

/// <summary>
/// Real-time channel for in-app notifications. Clients authenticate with JWT (query access_token or Bearer header).
/// Server sends client events <c>ReceiveNotification</c> (generic) and <c>DocumentApproved</c> (published after final approval).
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger) => _logger = logger;

    public const string ReceiveNotification = "ReceiveNotification";
    public const string DocumentApproved = "DocumentApproved";

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR notifications connected: {ConnectionId} user {UserId}",
            Context.ConnectionId, Context.UserIdentifier);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
            _logger.LogWarning(exception, "SignalR notifications disconnected with error");
        return base.OnDisconnectedAsync(exception);
    }
}
