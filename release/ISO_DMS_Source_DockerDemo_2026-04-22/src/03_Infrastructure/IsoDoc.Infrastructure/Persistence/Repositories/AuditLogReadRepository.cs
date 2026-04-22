using IsoDoc.Domain.Interfaces;
using IsoDoc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IsoDoc.Infrastructure.Persistence.Repositories;

public sealed class AuditLogReadRepository : IAuditLogReadRepository
{
    private readonly AppDbContext _db;

    public AuditLogReadRepository(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditLogReadModel> Items, int TotalCount)> SearchAsync(
        AuditLogSearchCriteria criteria,
        CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking();

        if (criteria.UserId.HasValue)
            q = q.Where(a => a.UserId == criteria.UserId);

        if (!string.IsNullOrWhiteSpace(criteria.Action))
        {
            var a = criteria.Action.Trim();
            q = q.Where(x => x.Action.Contains(a));
        }

        if (!string.IsNullOrWhiteSpace(criteria.EntityType))
        {
            var t = criteria.EntityType.Trim();
            q = q.Where(x => x.EntityType != null && x.EntityType.Contains(t));
        }

        if (!string.IsNullOrWhiteSpace(criteria.EntityId))
        {
            var id = criteria.EntityId.Trim();
            q = q.Where(x => x.EntityId != null && x.EntityId.Contains(id));
        }

        if (criteria.FromUtc.HasValue)
            q = q.Where(x => x.OccurredAt >= criteria.FromUtc.Value);

        if (criteria.ToUtc.HasValue)
            q = q.Where(x => x.OccurredAt <= criteria.ToUtc.Value);

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(x => x.OccurredAt)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(x => new AuditLogReadModel(
                x.Id,
                x.UserId,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.IpAddress,
                x.OccurredAt))
            .ToList();

        return (items, total);
    }
}
