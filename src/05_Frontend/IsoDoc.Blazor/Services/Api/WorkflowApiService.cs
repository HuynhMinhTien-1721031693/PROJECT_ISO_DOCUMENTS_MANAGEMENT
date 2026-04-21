using IsoDoc.Blazor.Models;

namespace IsoDoc.Blazor.Services.Api;

public sealed class WorkflowApiService
{
    private readonly ApiClient _apiClient;

    public WorkflowApiService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<(IReadOnlyList<PendingApprovalDto> Items, PaginationMeta? Pagination, string? Error)>
        GetPendingAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var (data, pagination, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<PendingApprovalDto>>(
            $"Workflow/pending?page={page}&pageSize={pageSize}",
            ct);

        return (data ?? Array.Empty<PendingApprovalDto>(), pagination, error);
    }

    public Task<(bool Ok, string? Error)> DecideAsync(
        Guid workflowId,
        string decision,
        string? comment,
        CancellationToken ct = default)
    {
        return _apiClient.PostWithoutContentAsync(
            $"Workflow/{workflowId}/decision",
            new { decision, comment },
            ct);
    }

    public async Task<(WorkflowDetailDto? Detail, string? Error)> GetDetailAsync(
        Guid workflowId,
        CancellationToken ct = default)
    {
        var (data, _, error) = await _apiClient.GetWrappedAsync<WorkflowDetailDto>($"Workflow/{workflowId}", ct);
        return (data, error);
    }
}
