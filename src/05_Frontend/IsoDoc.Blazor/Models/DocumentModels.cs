using System.Text.Json.Serialization;

namespace IsoDoc.Blazor.Models;

public sealed class DocumentSummaryDto
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
}

public sealed class DocumentVersionDto
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

public sealed class SubmitWorkflowResponseDto
{
    [JsonPropertyName("workflowId")]
    public Guid WorkflowId { get; init; }
}

public sealed class DocumentVersionCreatedDto
{
    [JsonPropertyName("documentId")]
    public Guid DocumentId { get; init; }

    [JsonPropertyName("versionId")]
    public Guid VersionId { get; init; }
}

public sealed class DocumentDownloadInfoDto
{
    public string Mode { get; init; } = string.Empty;
    public string? SasUrl { get; init; }
    public DateTimeOffset? SasExpiresAt { get; init; }
    public string? ApiRelativeUrl { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumHex { get; init; } = string.Empty;
}

public sealed class ApprovalStepDto
{
    public int StepOrder { get; init; }
    public string ApproverName { get; init; } = string.Empty;
    public string Decision { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public DateTime? DecidedAt { get; init; }
}

public sealed class WorkflowStatusDto
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

public sealed class DocumentDto
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

public sealed class PendingApprovalDto
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
    public int DaysWaiting { get; init; }
}

public sealed class SearchDocumentsParams
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? Category { get; set; }
    public string? Standard { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "UpdatedAt";
    public bool SortDesc { get; set; } = true;
}
