using IsoDoc.Domain.Common;

namespace IsoDoc.Domain.Entities;

/// <summary>
/// In-app notification persisted for a user (workflow, publish, reject, etc.).
/// </summary>
public sealed class UserNotification : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
}
