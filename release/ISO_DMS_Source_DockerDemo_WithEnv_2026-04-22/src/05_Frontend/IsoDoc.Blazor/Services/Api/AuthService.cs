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
        {
            if ((int)response.StatusCode == 423)
                return (false, "Tai khoan tam thoi bi khoa do nhap sai nhieu lan. Thu lai sau 15 phut hoac reset DB (docker compose down -v).");
            if ((int)response.StatusCode == 401)
                return (false, "Sai email hoac mat khau, hoac API chua seed tai khoan demo (can chay Docker / Development voi seed).");
            return (false, $"Dang nhap that bai ({(int)response.StatusCode}). Kiem tra API dang chay va dia chi Api:BaseUrl.");
        }

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
