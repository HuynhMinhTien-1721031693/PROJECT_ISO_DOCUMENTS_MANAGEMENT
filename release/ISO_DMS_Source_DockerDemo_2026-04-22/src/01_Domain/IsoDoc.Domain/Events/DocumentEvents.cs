using IsoDoc.Domain.Common;
using IsoDoc.Domain.ValueObjects;

namespace IsoDoc.Domain.Events;

// ── Document lifecycle events ────────────────────────────────────────────────

/// <summary>Raised when a new document is created (status = Draft).</summary>
public sealed record DocumentCreatedEvent(
    Guid DocumentId,
    DocumentCode DocumentCode,
    Guid OwnerId
) : DomainEventBase;

/// <summary>
/// Raised after persisted changes that affect search (metadata, workflow status, archive, etc.).
/// Handlers update Elasticsearch; SQL remains authoritative.
/// </summary>
public sealed record DocumentIndexSyncEvent(Guid DocumentId) : DomainEventBase;

/// <summary>Raised when a document is submitted to the approval workflow.</summary>
public sealed record DocumentSubmittedForReviewEvent(
    Guid DocumentId,
    Guid SubmittedBy
) : DomainEventBase;

/// <summary>
/// Raised when a document is fully approved and published.
/// Triggers: in-app notification (search index is updated via <see cref="DocumentIndexSyncEvent"/>).
/// </summary>
public sealed record DocumentPublishedEvent(
    Guid DocumentId,
    DocumentCode DocumentCode,
    VersionNumber Version,
    Guid ApprovedBy
) : DomainEventBase;

/// <summary>
/// Raised when a document is rejected at any review step.
/// Triggers: notification to document owner with rejection reason.
/// </summary>
public sealed record DocumentRejectedEvent(
    Guid DocumentId,
    Guid RejectedBy,
    string Reason
) : DomainEventBase;

/// <summary>Raised when a published document is archived (superseded).</summary>
public sealed record DocumentArchivedEvent(
    Guid DocumentId,
    Guid ArchivedBy
) : DomainEventBase;

// ── Workflow events ──────────────────────────────────────────────────────────

/// <summary>Raised when a workflow step is approved and the next approver needs to act.</summary>
public sealed record WorkflowStepAdvancedEvent(
    Guid DocumentId,
    Guid WorkflowId,
    Guid NextApproverId,
    int NextStepOrder
) : DomainEventBase;

/// <summary>Raised when all approval steps are completed successfully.</summary>
public sealed record WorkflowCompletedEvent(
    Guid DocumentId,
    Guid VersionId
) : DomainEventBase;

/// <summary>Raised when any workflow step is rejected.</summary>
public sealed record WorkflowRejectedEvent(
    Guid DocumentId,
    Guid VersionId,
    Guid RejectedBy,
    string Reason
) : DomainEventBase;

