using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace IsoDoc.Blazor.Services.Auth;

public sealed class JwtParser
{
    public ClaimsPrincipal ParsePrincipal(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Anonymous();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return Anonymous();
        }
    }

    public static bool IsExpired(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());
}
