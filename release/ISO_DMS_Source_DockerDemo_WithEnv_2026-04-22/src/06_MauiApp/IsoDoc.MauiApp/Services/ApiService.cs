using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace IsoDoc.MauiApp.Services;

public interface IApiService
{
    Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string relativePath, TRequest payload, CancellationToken cancellationToken = default);
}

public sealed class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly SecureStorageService _secureStorageService;

    public ApiService(HttpClient httpClient, SecureStorageService secureStorageService)
    {
        _httpClient = httpClient;
        _secureStorageService = secureStorageService;
    }

    public async Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default)
    {
        await AddAuthorizationHeaderAsync();
        return await _httpClient.GetFromJsonAsync<T>(relativePath, cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string relativePath, TRequest payload, CancellationToken cancellationToken = default)
    {
        await AddAuthorizationHeaderAsync();
        using var response = await _httpClient.PostAsJsonAsync(relativePath, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
    }

    private async Task AddAuthorizationHeaderAsync()
    {
        var accessToken = await _secureStorageService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
