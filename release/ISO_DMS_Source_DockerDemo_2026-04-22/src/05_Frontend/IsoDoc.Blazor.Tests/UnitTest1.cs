using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IsoDoc.Blazor.Services.Auth;

namespace IsoDoc.Blazor.Tests;

public class JwtParserTests
{
    [Fact]
    public void ParsePrincipal_ReturnsAuthenticatedIdentity_ForValidToken()
    {
        var parser = new JwtParser();
        var token = BuildToken(DateTime.UtcNow.AddMinutes(30), new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "admin@local"),
            new Claim(ClaimTypes.Role, "SystemAdmin")
        });

        var principal = parser.ParsePrincipal(token);

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("admin@local", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Contains(principal.Claims, x => x.Type == ClaimTypes.Role && x.Value == "SystemAdmin");
    }

    [Fact]
    public void IsExpired_ReturnsTrue_ForExpiredToken()
    {
        var token = BuildToken(DateTime.UtcNow.AddMinutes(-5), Array.Empty<Claim>());

        var expired = JwtParser.IsExpired(token);

        Assert.True(expired);
    }

    [Fact]
    public void IsExpired_ReturnsFalse_ForFreshToken()
    {
        var token = BuildToken(DateTime.UtcNow.AddMinutes(10), Array.Empty<Claim>());

        var expired = JwtParser.IsExpired(token);

        Assert.False(expired);
    }

    private static string BuildToken(DateTime validToUtc, IEnumerable<Claim> claims)
    {
        var notBefore = validToUtc.AddMinutes(-10);
        var jwt = new JwtSecurityToken(
            issuer: "unit-test",
            audience: "unit-test",
            claims: claims,
            notBefore: notBefore,
            expires: validToUtc);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
