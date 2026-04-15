using System.Security.Claims;
using IsoDoc.Blazor.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace IsoDoc.Blazor.Services.Auth;

public sealed class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenStorageService _tokenStorage;
    private readonly JwtParser _jwtParser;

    public CustomAuthStateProvider(TokenStorageService tokenStorage, JwtParser jwtParser)
    {
        _tokenStorage = tokenStorage;
        _jwtParser = jwtParser;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var tokens = await _tokenStorage.GetTokensAsync();
        var principal = _jwtParser.ParsePrincipal(tokens?.AccessToken);
        return new AuthenticationState(principal);
    }

    public void NotifyAuthenticated(LoginResponse response)
    {
        var principal = _jwtParser.ParsePrincipal(response.AccessToken);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
    }

    public void NotifyLoggedOut()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }
}
