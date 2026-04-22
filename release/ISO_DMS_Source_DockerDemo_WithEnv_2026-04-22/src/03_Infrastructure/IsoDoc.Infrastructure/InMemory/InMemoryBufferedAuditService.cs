using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryBufferedAuditService : IAuditService
{
    private readonly InMemoryAuditLogStore _store;
    private readonly ICurrentUserService _currentUser;

    public InMemoryBufferedAuditService(InMemoryAuditLogStore store, ICurrentUserService currentUser)
    {
        _store = store;
        _currentUser = currentUser;
    }

    public Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var row = new AuditLogReadModel(
            Guid.NewGuid(),
            userId ?? _currentUser.UserId,
            action,
            entityType,
            entityId,
            ipAddress,
            DateTime.UtcNow);

        _store.Append(row);
        return Task.CompletedTask;
    }
}
