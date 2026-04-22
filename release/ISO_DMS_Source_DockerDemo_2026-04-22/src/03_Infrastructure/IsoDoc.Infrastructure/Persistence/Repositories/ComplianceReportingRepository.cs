using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsoDoc.Infrastructure.Persistence.Repositories;

public sealed class ComplianceReportingRepository : IComplianceReportRepository
{
    private readonly AppDbContext _db;

    public ComplianceReportingRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DocumentStatusCountRow>> GetDocumentCountsByStatusAsync(
        CancellationToken ct = default)
        => await _db.Documents
            .AsNoTracking()
            .GroupBy(d => d.Status)
            .Select(g => new DocumentStatusCountRow(g.Key, g.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ApprovalSlaReportRow>> GetApprovalSlaRowsAsync(
        DateTime? completedFromUtc,
        DateTime? completedToUtc,
        CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;

        var joined = await (
            from w in _db.ApprovalWorkflows.AsNoTracking()
            join d in _db.Documents.AsNoTracking() on w.DocumentId equals d.Id
            orderby w.StartedAt descending
            select new { w, d }).ToListAsync(ct);

        var result = new List<ApprovalSlaReportRow>(joined.Count);

        foreach (var x in joined)
        {
            var w = x.w;
            if (!ShouldIncludeWorkflow(w, completedFromUtc, completedToUtc))
                continue;

            var closedHours = w.CompletedAt.HasValue
                ? (w.CompletedAt.Value - w.StartedAt).TotalHours
                : (double?)null;
            var openHours = w.CompletedAt is null ? (utcNow - w.StartedAt).TotalHours : (double?)null;

            result.Add(new ApprovalSlaReportRow(
                w.Id,
                w.DocumentId,
                x.d.Code.Value,
                x.d.Title,
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
