using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.ValueObjects;

namespace IsoDoc.Domain.Interfaces;

/// <summary>
/// Repository contract for Document aggregate.
/// Returns domain entities, not DTOs.
/// </summary>
public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Document?> GetByCodeAsync(DocumentCode code, CancellationToken ct = default);
    Task<bool> ExistsAsync(DocumentCode code, CancellationToken ct = default);

    Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>All non-deleted documents for compliance reporting (status histograms).</summary>
    Task<IReadOnlyList<Document>> GetAllForReportingAsync(CancellationToken ct = default);

    /// <summary>
    /// Relational search when Elasticsearch is down (code, title, description, tags, filters, dates).
    /// </summary>
    Task<SearchResult> SearchDocumentsAsync(SearchQuery query, CancellationToken ct = default);

    Task AddAsync(Document document, CancellationToken ct = default);
    void Update(Document document);
    void Remove(Document document);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IApprovalWorkflowRepository
{
    Task<ApprovalWorkflow?> GetByIdAsync(Guid workflowId, CancellationToken ct = default);
    Task<ApprovalWorkflow?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default);
    Task<ApprovalWorkflow?> GetActiveWorkflowAsync(Guid documentId, CancellationToken ct = default);
    Task<ApprovalWorkflow?> GetLatestWorkflowForDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalWorkflow>> GetPendingForApproverAsync(Guid approverId, CancellationToken ct = default);

    /// <summary>Workflow instances for SLA / cycle-time reporting.</summary>
    Task<IReadOnlyList<ApprovalWorkflow>> GetAllForReportingAsync(CancellationToken ct = default);

    Task AddAsync(ApprovalWorkflow workflow, CancellationToken ct = default);
    void Update(ApprovalWorkflow workflow);
}

public interface IUserNotificationRepository
{
    Task<UserNotification> AddAsync(UserNotification notification, CancellationToken ct = default);
    Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> GetPageForUserAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken ct = default);
    Task<UserNotification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Immutable audit row as returned from the read store.</summary>
public sealed record AuditLogReadModel(
    Guid Id,
    Guid? UserId,
    string Action,
    string? EntityType,
    string? EntityId,
    string? IpAddress,
    DateTime OccurredAtUtc);

public sealed record AuditLogSearchCriteria(
    Guid? UserId,
    string? Action,
    string? EntityType,
    string? EntityId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);

public interface IAuditLogReadRepository
{
    Task<(IReadOnlyList<AuditLogReadModel> Items, int TotalCount)> SearchAsync(
        AuditLogSearchCriteria criteria,
        CancellationToken ct = default);
}

public sealed record DocumentStatusCountRow(DocumentStatus Status, int Count);

public sealed record ApprovalSlaReportRow(
    Guid WorkflowId,
    Guid DocumentId,
    string DocumentCode,
    string DocumentTitle,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    WorkflowStatus WorkflowStatus,
    double? ClosedCycleHours,
    double? OpenWaitingHours);

public interface IComplianceReportRepository
{
    Task<IReadOnlyList<DocumentStatusCountRow>> GetDocumentCountsByStatusAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalSlaReportRow>> GetApprovalSlaRowsAsync(
        DateTime? completedFromUtc,
        DateTime? completedToUtc,
        CancellationToken ct = default);
}

/// <summary>
/// File storage abstraction. Implemented in Infrastructure.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Uploads file and returns blob path (storage key).</summary>
    Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        CancellationToken ct = default);

    /// <summary>Returns a time-limited SAS URL for secure download.</summary>
    Task<string> GetSecureDownloadUrlAsync(
        string blobPath,
        TimeSpan expiry,
        CancellationToken ct = default);

    /// <summary>Opens a read stream to the stored object (for authenticated proxy download).</summary>
    Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct = default);

    /// <summary>True when <see cref="GetSecureDownloadUrlAsync"/> can produce a public time-limited URL.</summary>
    bool SupportsTimeLimitedPublicUrls { get; }

    Task DeleteAsync(string blobPath, CancellationToken ct = default);
}

/// <summary>
/// Full-text search service abstraction. Implemented by Elasticsearch/Azure Search in Infrastructure.
/// </summary>
public interface ISearchService
{
    Task IndexDocumentAsync(Document document, CancellationToken ct = default);
    Task UpdateDocumentIndexAsync(Document document, CancellationToken ct = default);
    Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct = default);
}

/// <summary>
/// Audit logging service. Writes to immutable AuditLogs table.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        object? oldValues = null,
        object? newValues = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Email/notification service for workflow events.
/// </summary>
public interface INotificationService
{
    Task SendWorkflowNotificationAsync(
        Guid recipientUserId,
        string subject,
        string message,
        CancellationToken ct = default);
}

/// <summary>
/// Search query parameters owned by domain.
/// </summary>
public record SearchQuery(
    string? Keyword = null,
    IsoStandard? Standard = null,
    DocumentStatus? Status = null,
    DocumentCategory? Category = null,
    Guid? OwnerId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "UpdatedAt",
    bool SortDesc = true);

public record SearchResult(
    IReadOnlyList<SearchHit> Hits,
    int TotalCount,
    int Page,
    int PageSize);

public record SearchHit(
    Guid DocumentId,
    string DocumentCode,
    string Title,
    string Status,
    string Category,
    string Standard,
    DateTime UpdatedAt,
    double Score,
    IReadOnlyList<string> Highlights);

