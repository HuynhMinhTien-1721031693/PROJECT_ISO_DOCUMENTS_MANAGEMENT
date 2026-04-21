using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IsoDoc.Infrastructure.Notifications;

[Authorize]
public sealed class NotificationHub : Hub;
