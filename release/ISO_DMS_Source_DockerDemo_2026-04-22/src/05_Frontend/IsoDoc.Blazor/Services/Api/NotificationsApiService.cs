using IsoDoc.Blazor.Models;

namespace IsoDoc.Blazor.Services.Api;

public sealed class NotificationsApiService
{
    private readonly ApiClient _apiClient;

    public NotificationsApiService(ApiClient apiClient) => _apiClient = apiClient;

    public async Task<(IReadOnlyList<UserNotificationDto> Items, PaginationMeta? Pagination, string? Error)>
        GetListAsync(int page = 1, int pageSize = 20, bool unreadOnly = false, CancellationToken ct = default)
    {
        var q = $"Notifications?page={page}&pageSize={pageSize}&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}";
        var (data, pagination, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<UserNotificationDto>>(q, ct);
        return (data ?? Array.Empty<UserNotificationDto>(), pagination, error);
    }

    public async Task<(int Count, string? Error)> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var (data, _, error) = await _apiClient.GetWrappedAsync<int>("Notifications/unread-count", ct);
        return (data, error);
    }

    public Task<(bool Ok, string? Error)> MarkReadAsync(Guid id, CancellationToken ct = default)
        => _apiClient.PostWithoutContentAsync($"Notifications/{id}/read", new { }, ct);

    public Task<(bool Ok, string? Error)> MarkAllReadAsync(CancellationToken ct = default)
        => _apiClient.PostWithoutContentAsync("Notifications/read-all", new { }, ct);
}
