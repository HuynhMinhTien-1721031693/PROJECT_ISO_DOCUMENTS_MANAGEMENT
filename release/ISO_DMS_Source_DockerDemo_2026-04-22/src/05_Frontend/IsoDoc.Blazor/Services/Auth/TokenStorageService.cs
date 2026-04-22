using IsoDoc.Blazor.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Cryptography;

namespace IsoDoc.Blazor.Services.Auth;

public sealed class TokenStorageService
{
    private const string AccessTokenKey = "isodoc_access_token";
    private const string RefreshTokenKey = "isodoc_refresh_token";
    private readonly ProtectedLocalStorage _localStorage;

    public TokenStorageService(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<TokenBundle?> GetTokensAsync()
    {
        try
        {
            var access = await _localStorage.GetAsync<string>(AccessTokenKey);
            var refresh = await _localStorage.GetAsync<string>(RefreshTokenKey);
            if (!access.Success || !refresh.Success || string.IsNullOrWhiteSpace(access.Value))
                return null;

            return new TokenBundle
            {
                AccessToken = access.Value!,
                RefreshToken = refresh.Value ?? string.Empty
            };
        }
        catch (InvalidOperationException)
        {
            // During prerender JS interop is unavailable; return anonymous state.
            return null;
        }
        catch (CryptographicException)
        {
            // Key ring changed (e.g. container recreated) and old protected values cannot be decrypted.
            // Clear stale tokens to recover instead of crashing the Blazor circuit.
            await ClearAsync();
            return null;
        }
    }

    public async Task SetTokensAsync(LoginResponse response)
    {
        await _localStorage.SetAsync(AccessTokenKey, response.AccessToken);
        await _localStorage.SetAsync(RefreshTokenKey, response.RefreshToken);
    }

    public async Task ClearAsync()
    {
        await _localStorage.DeleteAsync(AccessTokenKey);
        await _localStorage.DeleteAsync(RefreshTokenKey);
    }
}
