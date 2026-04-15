using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class NoOpAuditService : IAuditService
{
    public Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        CancellationToken ct = default)
        => Task.CompletedTask;
}
