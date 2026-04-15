namespace IsoDoc.MauiApp.Services;

public sealed class SecureStorageService
{
    private const string AccessTokenKey = "isodoc_access_token";
    private const string RefreshTokenKey = "isodoc_refresh_token";

    public Task<string?> GetAccessTokenAsync()
        => SecureStorage.Default.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync()
        => SecureStorage.Default.GetAsync(RefreshTokenKey);

    public async Task SaveTokensAsync(string accessToken, string? refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
        }
    }

    public void Clear()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
    }
}
