using System.Net.Http.Json;
using IsoDoc.Blazor.Models;

namespace IsoDoc.Blazor.Services.Api;

public sealed class AdminUsersApiService
{
    private readonly ApiClient _apiClient;

    public AdminUsersApiService(ApiClient apiClient) => _apiClient = apiClient;

    public async Task<(IReadOnlyList<UserListItemDto> Items, PaginationMeta? Pagination, string? Error)> ListAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken ct = default)
    {
        var q = $"admin/users?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            q += $"&search={Uri.EscapeDataString(search)}";

        var (data, pagination, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<UserListItemDto>>(q, ct);
        return (data ?? Array.Empty<UserListItemDto>(), pagination, error);
    }

    public async Task<(bool Ok, string? Error)> SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default) =>
        await _apiClient.PutWithoutContentAsync($"admin/users/{userId}/roles", new { roles }, ct);

    public async Task<(UserListItemDto? Created, string? Error)> CreateAsync(
        string email,
        string password,
        string? displayName,
        Guid? departmentId,
        IReadOnlyList<string> roles,
        CancellationToken ct = default)
    {
        var content = JsonContent.Create(new
        {
            email,
            password,
            displayName,
            departmentId,
            roles
        });
        return await _apiClient.PostWrappedAsync<UserListItemDto>("admin/users", content, ct);
    }

    public Task<(bool Ok, string? Error)> LockAsync(Guid userId, CancellationToken ct = default) =>
        _apiClient.PostWithoutContentAsync($"admin/users/{userId}/lock", new { }, ct);

    public Task<(bool Ok, string? Error)> UnlockAsync(Guid userId, CancellationToken ct = default) =>
        _apiClient.PostWithoutContentAsync($"admin/users/{userId}/unlock", new { }, ct);

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid userId, CancellationToken ct = default) =>
        _apiClient.DeleteAsync($"admin/users/{userId}", ct);
}
