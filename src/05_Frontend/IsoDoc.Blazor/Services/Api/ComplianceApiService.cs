using IsoDoc.Blazor.Models;

namespace IsoDoc.Blazor.Services.Api;

public sealed class ComplianceApiService
{
    private readonly ApiClient _apiClient;

    public ComplianceApiService(ApiClient apiClient) => _apiClient = apiClient;

    public async Task<(IReadOnlyList<AuditLogDto> Items, PaginationMeta? Pagination, string? Error)> SearchAuditLogsAsync(
        AuditLogSearchParams p,
        CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"page={p.Page}",
            $"pageSize={p.PageSize}"
        };

        if (p.UserId.HasValue)
            query.Add($"userId={p.UserId.Value}");
        if (!string.IsNullOrWhiteSpace(p.Action))
            query.Add($"action={Uri.EscapeDataString(p.Action)}");
        if (!string.IsNullOrWhiteSpace(p.EntityType))
            query.Add($"entityType={Uri.EscapeDataString(p.EntityType)}");
        if (!string.IsNullOrWhiteSpace(p.EntityId))
            query.Add($"entityId={Uri.EscapeDataString(p.EntityId)}");
        if (p.FromUtc.HasValue)
            query.Add($"fromUtc={Uri.EscapeDataString(p.FromUtc.Value.ToString("o"))}");
        if (p.ToUtc.HasValue)
            query.Add($"toUtc={Uri.EscapeDataString(p.ToUtc.Value.ToString("o"))}");

        var (data, pagination, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<AuditLogDto>>(
            $"AuditLogs?{string.Join("&", query)}",
            ct);
        return (data ?? Array.Empty<AuditLogDto>(), pagination, error);
    }

    public async Task<(byte[]? Data, string? Error)> ExportAuditCsvAsync(
        AuditLogSearchParams p,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (p.UserId.HasValue)
            query.Add($"userId={p.UserId.Value}");
        if (!string.IsNullOrWhiteSpace(p.Action))
            query.Add($"action={Uri.EscapeDataString(p.Action)}");
        if (!string.IsNullOrWhiteSpace(p.EntityType))
            query.Add($"entityType={Uri.EscapeDataString(p.EntityType)}");
        if (!string.IsNullOrWhiteSpace(p.EntityId))
            query.Add($"entityId={Uri.EscapeDataString(p.EntityId)}");
        if (p.FromUtc.HasValue)
            query.Add($"fromUtc={Uri.EscapeDataString(p.FromUtc.Value.ToString("o"))}");
        if (p.ToUtc.HasValue)
            query.Add($"toUtc={Uri.EscapeDataString(p.ToUtc.Value.ToString("o"))}");

        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
        return await _apiClient.GetBytesAsync($"AuditLogs/export{qs}", ct);
    }

    public async Task<(IReadOnlyList<DocumentStatusReportItemDto>? Data, string? Error)> GetDocumentStatusReportAsync(
        CancellationToken ct = default)
    {
        var (data, _, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<DocumentStatusReportItemDto>>(
            "reports/document-status",
            ct);
        return (data, error);
    }

    public async Task<(byte[]? Data, string? Error)> ExportDocumentStatusCsvAsync(CancellationToken ct = default)
        => await _apiClient.GetBytesAsync("reports/document-status/export", ct);

    public async Task<(IReadOnlyList<ApprovalSlaReportItemDto>? Data, string? Error)> GetApprovalSlaReportAsync(
        DateTime? completedFromUtc,
        DateTime? completedToUtc,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (completedFromUtc.HasValue)
            query.Add($"completedFromUtc={Uri.EscapeDataString(completedFromUtc.Value.ToString("o"))}");
        if (completedToUtc.HasValue)
            query.Add($"completedToUtc={Uri.EscapeDataString(completedToUtc.Value.ToString("o"))}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";

        var (data, _, error) = await _apiClient.GetWrappedAsync<IReadOnlyList<ApprovalSlaReportItemDto>>(
            $"reports/approval-sla{qs}",
            ct);
        return (data, error);
    }

    public async Task<(byte[]? Data, string? Error)> ExportApprovalSlaCsvAsync(
        DateTime? completedFromUtc,
        DateTime? completedToUtc,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (completedFromUtc.HasValue)
            query.Add($"completedFromUtc={Uri.EscapeDataString(completedFromUtc.Value.ToString("o"))}");
        if (completedToUtc.HasValue)
            query.Add($"completedToUtc={Uri.EscapeDataString(completedToUtc.Value.ToString("o"))}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";

        return await _apiClient.GetBytesAsync($"reports/approval-sla/export{qs}", ct);
    }
}
