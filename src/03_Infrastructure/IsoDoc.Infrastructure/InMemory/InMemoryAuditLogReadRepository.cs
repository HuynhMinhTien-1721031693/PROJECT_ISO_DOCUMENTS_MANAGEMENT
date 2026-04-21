using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryAuditLogReadRepository : IAuditLogReadRepository
{
    private readonly InMemoryAuditLogStore _store;

    public InMemoryAuditLogReadRepository(InMemoryAuditLogStore store) => _store = store;

    public Task<(IReadOnlyList<AuditLogReadModel> Items, int TotalCount)> SearchAsync(
        AuditLogSearchCriteria criteria,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Search(criteria));
    }
}
