namespace IsoDoc.Application.Common.Models;

public sealed record UserNotificationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ActionUrl { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record WorkflowDetailDto
{
    public Guid DocumentId { get; init; }
    public string DocumentCode { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public WorkflowStatusDto Workflow { get; init; } = null!;
}
