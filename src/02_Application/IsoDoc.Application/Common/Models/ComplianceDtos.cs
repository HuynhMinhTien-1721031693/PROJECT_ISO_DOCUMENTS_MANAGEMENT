namespace IsoDoc.Application.Common.Models;

public sealed record AuditLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? IpAddress { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}

public sealed record DocumentStatusReportItemDto
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed record ApprovalSlaReportItemDto
{
    public Guid WorkflowId { get; init; }
    public Guid DocumentId { get; init; }
    public string DocumentCode { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string WorkflowStatus { get; init; } = string.Empty;
    public double? ClosedCycleHours { get; init; }
    public double? OpenWaitingHours { get; init; }
}
