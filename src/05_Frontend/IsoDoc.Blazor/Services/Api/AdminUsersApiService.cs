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
}
