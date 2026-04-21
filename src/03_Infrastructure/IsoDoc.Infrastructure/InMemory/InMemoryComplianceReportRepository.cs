using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryComplianceReportRepository : IComplianceReportRepository
{
    private readonly IDocumentRepository _documents;
    private readonly IApprovalWorkflowRepository _workflows;

    public InMemoryComplianceReportRepository(
        IDocumentRepository documents,
        IApprovalWorkflowRepository workflows)
    {
        _documents = documents;
        _workflows = workflows;
    }

    public async Task<IReadOnlyList<DocumentStatusCountRow>> GetDocumentCountsByStatusAsync(
        CancellationToken ct = default)
    {
        var docs = await _documents.GetAllForReportingAsync(ct);
        return docs
            .GroupBy(d => d.Status)
            .Select(g => new DocumentStatusCountRow(g.Key, g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<ApprovalSlaReportRow>> GetApprovalSlaRowsAsync(
        DateTime? completedFromUtc,
        DateTime? completedToUtc,
        CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        var workflows = await _workflows.GetAllForReportingAsync(ct);
        var documents = await _documents.GetAllForReportingAsync(ct);
        var docById = documents.ToDictionary(d => d.Id);

        var result = new List<ApprovalSlaReportRow>();

        foreach (var w in workflows)
        {
            if (!docById.TryGetValue(w.DocumentId, out var doc))
                continue;

            if (!ShouldIncludeWorkflow(w, completedFromUtc, completedToUtc))
                continue;

            var closedHours = w.CompletedAt.HasValue
                ? (w.CompletedAt.Value - w.StartedAt).TotalHours
                : (double?)null;
            var openHours = w.CompletedAt is null ? (utcNow - w.StartedAt).TotalHours : (double?)null;

            result.Add(new ApprovalSlaReportRow(
                w.Id,
                w.DocumentId,
                doc.Code.Value,
                doc.Title,
                w.StartedAt,
                w.CompletedAt,
                w.Status,
                closedHours,
                openHours));
        }

        return result;
    }

    private static bool ShouldIncludeWorkflow(
        ApprovalWorkflow w,
        DateTime? completedFromUtc,
        DateTime? completedToUtc)
    {
        if (w.Status == WorkflowStatus.InProgress)
            return true;

        if (w.CompletedAt is null)
            return false;

        if (!completedFromUtc.HasValue && !completedToUtc.HasValue)
            return true;

        var c = w.CompletedAt.Value;
        if (completedFromUtc.HasValue && c < completedFromUtc.Value)
            return false;
        if (completedToUtc.HasValue && c > completedToUtc.Value)
            return false;

        return true;
    }
}
