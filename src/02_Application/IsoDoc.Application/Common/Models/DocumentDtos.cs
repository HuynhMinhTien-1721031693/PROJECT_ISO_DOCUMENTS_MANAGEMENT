namespace IsoDoc.Application.Common.Models;

public sealed record DocumentDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string DocumentCode { get; init; } = string.Empty;
    public string IsoStandard { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public Guid OwnerId { get; init; }
    public string OwnerName { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<DocumentVersionDto> Versions { get; init; } = Array.Empty<DocumentVersionDto>();
    public WorkflowStatusDto? ActiveWorkflow { get; init; }
}

public sealed record DocumentSummaryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string DocumentCode { get; init; } = string.Empty;
    public string IsoStandard { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<string> HighlightedFragments { get; init; } = Array.Empty<string>();
}

public sealed record DocumentVersionDto
{
    public Guid Id { get; init; }
    public string BlobPath { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string FileType { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string FileSizeFormatted { get; init; } = string.Empty;
    public string ChecksumHex { get; init; } = string.Empty;
    public string? ChangeNote { get; init; }
    public string UploadedByName { get; init; } = string.Empty;
    public DateTime UploadedAt { get; init; }
    public bool IsCurrentVersion { get; init; }
}

/// <summary>Resolved file metadata for streaming download (internal to API layer).</summary>
public sealed record DocumentFileMetadataDto(
    string BlobPath,
    string DownloadFileName,
    string ContentType,
    long FileSize,
    string ChecksumHex);

/// <summary>How the client should obtain bytes: SAS URL or authenticated API stream.</summary>
public sealed record DocumentDownloadInfoDto(
    string Mode,
    string? SasUrl,
    DateTimeOffset? SasExpiresAt,
    string? ApiRelativeUrl,
    string FileName,
    string ContentType,
    long FileSize,
    string ChecksumHex);

public sealed record WorkflowStatusDto
{
    public Guid WorkflowId { get; init; }
    public string Status { get; init; } = string.Empty;
    public int CurrentStepOrder { get; init; }
    public int TotalSteps { get; init; }
    public string? CurrentApproverName { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public IReadOnlyList<ApprovalStepDto> Steps { get; init; } = Array.Empty<ApprovalStepDto>();
}

public sealed record ApprovalStepDto
{
    public int StepOrder { get; init; }
    public string ApproverName { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public DateTime? DecidedAt { get; init; }
}

public sealed record PendingApprovalDto
{
    public Guid WorkflowId { get; init; }
    public Guid DocumentId { get; init; }
    public string DocumentCode { get; init; } = string.Empty;
    public string DocumentTitle { get; init; } = string.Empty;
    public string IsoStandard { get; init; } = string.Empty;
    public string SubmittedByName { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public int StepOrder { get; init; }
    public int TotalSteps { get; init; }
    public int DaysWaiting => (int)(DateTime.UtcNow - SubmittedAt).TotalDays;
}

