using System.Text.Json;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.Persistence;

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            UserId = userId ?? _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is not null ? JsonSerializer.Serialize(oldValues) : null,
            NewValues = newValues is not null ? JsonSerializer.Serialize(newValues) : null,
            IpAddress = ipAddress,
            OccurredAt = DateTime.UtcNow
        };

        await _db.AuditLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }
}
