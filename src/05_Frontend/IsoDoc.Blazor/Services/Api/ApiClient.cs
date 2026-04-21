using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IsoDoc.Blazor.Models;
using IsoDoc.Blazor.Services.Auth;

namespace IsoDoc.Blazor.Services.Api;

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;
    private readonly AuthService _authService;

    public ApiClient(HttpClient httpClient, TokenStorageService tokenStorage, AuthService authService)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authService = authService;
    }

    public async Task<(T? Data, PaginationMeta? Pagination, string? Error)> GetWrappedAsync<T>(
        string path,
        CancellationToken ct = default)
    {
        return await SendWrappedAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, path), ct);
    }

    public async Task<(byte[]? Data, string? Error)> GetBytesAsync(string path, CancellationToken ct = default)
    {
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), ct);
        if (!response.IsSuccessStatusCode)
            return (null, await response.Content.ReadAsStringAsync(ct));

        return (await response.Content.ReadAsByteArrayAsync(ct), null);
    }

    public async Task<(T? Data, string? Error)> PostWrappedAsync<T>(
        string path,
        HttpContent content,
        CancellationToken ct = default)
    {
        var (data, _, error) = await SendWrappedAsync<T>(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = content };
            return request;
        }, ct);

        return (data, error);
    }

    public async Task<(T? Data, string? Error)> PostWrappedEmptyAsync<T>(string path, CancellationToken ct = default)
    {
        var (data, _, error) = await SendWrappedAsync<T>(() => new HttpRequestMessage(HttpMethod.Post, path), ct);
        return (data, error);
    }

    public async Task<(bool Ok, string? Error)> PostWithoutContentAsync(
        string path,
        object payload,
        CancellationToken ct = default)
    {
        var response = await SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(payload)
            };
            return request;
        }, ct);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
            return (true, null);

        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, body);
    }

    public async Task<(bool Ok, string? Error)> PutJsonAsync(
        string path,
        object payload,
        CancellationToken ct = default)
    {
        var response = await SendAsync(() =>
            new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(payload) }, ct);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
            return (true, null);

        return (false, await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(string path, CancellationToken ct = default)
    {
        var response = await SendAsync(() => new HttpRequestMessage(HttpMethod.Delete, path), ct);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
            return (true, null);

        return (false, await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<(bool Ok, string? Error)> PutWithoutContentAsync(
        string path,
        object payload,
        CancellationToken ct = default)
    {
        var response = await SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = JsonContent.Create(payload)
            };
            return request;
        }, ct);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent)
            return (true, null);

        var body = await response.Content.ReadAsStringAsync(ct);
        return (false, body);
    }

    public async Task EnsureAuthorizationHeaderAsync(HttpRequestMessage request)
    {
        var token = (await _tokenStorage.GetTokensAsync())?.AccessToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<(T? Data, PaginationMeta? Pagination, string? Error)> SendWrappedAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        var response = await SendAsync(requestFactory, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return (default, null, body);
        }

        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: ct);
        return wrapped is null
            ? (default, null, "Invalid response body.")
            : (wrapped.Data, wrapped.Pagination, null);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        var request = requestFactory();
        await EnsureAuthorizationHeaderAsync(request);
        var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        var refreshed = await _authService.TryRefreshAsync(ct);
        if (!refreshed)
            return response;

        var retryRequest = requestFactory();
        await EnsureAuthorizationHeaderAsync(retryRequest);
        return await _httpClient.SendAsync(retryRequest, ct);
    }
}
