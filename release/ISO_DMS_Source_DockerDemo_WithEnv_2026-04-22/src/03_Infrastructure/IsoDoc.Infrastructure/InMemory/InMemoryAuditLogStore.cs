using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.InMemory;

/// <summary>
/// Process-local append-only buffer used when SQL Server is not configured (dev/demo).
/// </summary>
public sealed class InMemoryAuditLogStore
{
    private readonly List<AuditLogReadModel> _rows = new();
    private readonly object _gate = new();

    public void Append(AuditLogReadModel row)
    {
        lock (_gate)
        {
            _rows.Add(row);
        }
    }

    public (IReadOnlyList<AuditLogReadModel> Items, int TotalCount) Search(AuditLogSearchCriteria criteria)
    {
        lock (_gate)
        {
            IEnumerable<AuditLogReadModel> q = _rows.OrderByDescending(x => x.OccurredAtUtc);

            if (criteria.UserId.HasValue)
                q = q.Where(x => x.UserId == criteria.UserId);

            if (!string.IsNullOrWhiteSpace(criteria.Action))
            {
                var term = criteria.Action.Trim();
                q = q.Where(x => x.Action.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(criteria.EntityType))
            {
                var t = criteria.EntityType.Trim();
                q = q.Where(x => x.EntityType is not null
                    && x.EntityType.Contains(t, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(criteria.EntityId))
            {
                var id = criteria.EntityId.Trim();
                q = q.Where(x => x.EntityId is not null
                    && x.EntityId.Contains(id, StringComparison.OrdinalIgnoreCase));
            }

            if (criteria.FromUtc.HasValue)
                q = q.Where(x => x.OccurredAtUtc >= criteria.FromUtc.Value);

            if (criteria.ToUtc.HasValue)
                q = q.Where(x => x.OccurredAtUtc <= criteria.ToUtc.Value);

            var materialized = q.ToList();
            var total = materialized.Count;
            var page = Math.Max(1, criteria.Page);
            var pageSize = Math.Clamp(criteria.PageSize, 1, 200);
            var slice = materialized
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (slice, total);
        }
    }
}
