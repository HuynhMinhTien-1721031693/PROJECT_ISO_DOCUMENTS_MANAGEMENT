using System.Net.Http.Json;
using IsoDoc.Blazor.Models;
using IsoDoc.Blazor.Services.Auth;

namespace IsoDoc.Blazor.Services.Api;

public sealed class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;
    private readonly CustomAuthStateProvider _authStateProvider;

    public AuthService(HttpClient httpClient, TokenStorageService tokenStorage, CustomAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<(bool ok, string error)> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var response = await _httpClient.PostAsJsonAsync("Auth/login", request, ct);
        if (!response.IsSuccessStatusCode)
            return (false, $"Login failed ({(int)response.StatusCode}).");

        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(cancellationToken: ct);
        if (wrapped?.Data is null)
            return (false, "Invalid login response from server.");

        await _tokenStorage.SetTokensAsync(wrapped.Data);
        _authStateProvider.NotifyAuthenticated(wrapped.Data);
        return (true, string.Empty);
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        var current = await _tokenStorage.GetTokensAsync();
        if (current is null || string.IsNullOrWhiteSpace(current.RefreshToken))
            return false;

        var response = await _httpClient.PostAsJsonAsync(
            "Auth/refresh",
            new RefreshRequest { RefreshToken = current.RefreshToken },
            ct);

        if (!response.IsSuccessStatusCode)
            return false;

        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(cancellationToken: ct);
        if (wrapped?.Data is null)
            return false;

        await _tokenStorage.SetTokensAsync(wrapped.Data);
        _authStateProvider.NotifyAuthenticated(wrapped.Data);
        return true;
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            var current = await _tokenStorage.GetTokensAsync();
            if (current is not null && !string.IsNullOrWhiteSpace(current.RefreshToken))
            {
                await _httpClient.PostAsJsonAsync(
                    "Auth/logout",
                    new RefreshRequest { RefreshToken = current.RefreshToken },
                    ct);
            }
        }
        catch
        {
            // Ignore logout network errors and always clear local auth state.
        }
        finally
        {
            await _tokenStorage.ClearAsync();
            _authStateProvider.NotifyLoggedOut();
        }
    }
}
