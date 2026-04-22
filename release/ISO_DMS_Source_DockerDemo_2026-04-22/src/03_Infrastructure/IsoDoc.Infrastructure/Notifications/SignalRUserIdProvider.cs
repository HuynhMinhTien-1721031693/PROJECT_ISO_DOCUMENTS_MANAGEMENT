using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace IsoDoc.Infrastructure.Notifications;

/// <summary>Maps SignalR connections to per-user hub targets (JWT sub / nameidentifier).</summary>
public sealed class SignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? connection.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
}
