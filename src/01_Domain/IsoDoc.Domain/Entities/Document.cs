using System;
using System.Collections.Generic;
using System.Linq;
using IsoDoc.Domain.Common;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Events;
using IsoDoc.Domain.Exceptions;
using IsoDoc.Domain.ValueObjects;

namespace IsoDoc.Domain.Entities;

/// <summary>
/// Document aggregate root — the central entity of the entire domain.
/// </summary>
public sealed class Document : BaseAuditableEntity
{
    // Non-readonly so EF Core can materialize/replace the collection when loading aggregates.
    private List<DocumentVersion> _versions = new();

    public string Title { get; private set; } = string.Empty;
    public DocumentCode Code { get; private set; } = null!;
    public IsoStandard Standard { get; private set; }
    public DocumentCategory Category { get; private set; }
    public DocumentStatus Status { get; private set; }
    public VersionNumber CurrentVersion { get; private set; } = null!;

    public Guid OwnerId { get; private set; }
    public Guid DepartmentId { get; private set; }

    public string? Description { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; } = new List<string>();

    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    private Document()
    {
        // For ORM
    }

    public static Document Create(
        string title,
        string code,
        IsoStandard standard,
        DocumentCategory category,
        Guid ownerId,
        Guid departmentId,
        string? description = null,
        IEnumerable<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Document title cannot be empty.");
        if (title.Length > 500)
            throw new DomainException("Document title cannot exceed 500 characters.");
        if (ownerId == Guid.Empty)
            throw new DomainException("OwnerId is required.");
        if (departmentId == Guid.Empty)
            throw new DomainException("DepartmentId is required.");

        var doc = new Document
        {
            Title = title.Trim(),
            Code = DocumentCode.Create(code),
            Standard = standard,
            Category = category,
            Status = DocumentStatus.Draft,
            CurrentVersion = VersionNumber.Initial,
            OwnerId = ownerId,
            DepartmentId = departmentId,
            Description = description,
            Tags = (tags?.ToList() ?? new List<string>()).AsReadOnly()
        };

        doc.AddDomainEvent(new DocumentCreatedEvent(doc.Id, doc.Code, ownerId));
        return doc;
    }

    // ── State transition methods ─────────────────────────────────────────────

    /// <summary>Submit to approval workflow. Draft → UnderReview.</summary>
    public void SubmitForReview(Guid submittedBy)
    {
        EnsureStatus(DocumentStatus.Draft, "Only documents in Draft status can be submitted for review.");

        if (!_versions.Any())
            throw new DomainException("Cannot submit a document with no uploaded file. Please upload a file first.");

        Status = DocumentStatus.UnderReview;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = submittedBy;

        AddDomainEvent(new DocumentSubmittedForReviewEvent(Id, submittedBy));
    }

    /// <summary>QA/Safety/ISO officer approves → moves to PendingFinalApproval.</summary>
    public void AdvanceToFinalApproval(Guid approvedBy)
    {
        EnsureStatus(DocumentStatus.UnderReview, "Document must be under review to advance to final approval.");

        Status = DocumentStatus.PendingFinalApproval;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = approvedBy;
    }

    /// <summary>ISO Manager final sign-off. PendingFinalApproval → Published.</summary>
    public void Publish(Guid approvedBy, bool isMajorChange = false)
    {
        EnsureStatus(DocumentStatus.PendingFinalApproval,
            "Document must be pending final approval before it can be published.");

        CurrentVersion = isMajorChange ? CurrentVersion.BumpMajor() : CurrentVersion.BumpMinor();

        var latestVersion = _versions.OrderByDescending(v => v.UploadedAt).FirstOrDefault();
        latestVersion?.SetAsCurrent();

        Status = DocumentStatus.Published;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = approvedBy;

        AddDomainEvent(new DocumentPublishedEvent(Id, Code, CurrentVersion, approvedBy));
    }

    /// <summary>Reject at any review stage → Rejected (back to owner for revision).</summary>
    public void Reject(Guid rejectedBy, string reason)
    {
        if (Status is not (DocumentStatus.UnderReview or DocumentStatus.PendingFinalApproval))
            throw new InvalidDocumentWorkflowStateException(Id, Status, DocumentStatus.Rejected,
                "Documents can only be rejected while under review or pending final approval.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A rejection reason must be provided.");

        Status = DocumentStatus.Rejected;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = rejectedBy;

        AddDomainEvent(new DocumentRejectedEvent(Id, rejectedBy, reason));
    }

    /// <summary>Return a Rejected document to Draft for revision.</summary>
    public void ReturnToDraft(Guid revisedBy)
    {
        EnsureStatus(DocumentStatus.Rejected, "Only rejected documents can be returned to draft.");

        Status = DocumentStatus.Draft;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = revisedBy;
    }

    /// <summary>Archive a Published document (superseded by newer version).</summary>
    public void Archive(Guid archivedBy)
    {
        EnsureStatus(DocumentStatus.Published, "Only published documents can be archived.");

        Status = DocumentStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = archivedBy;

        AddDomainEvent(new DocumentArchivedEvent(Id, archivedBy));
    }

    // ── Version management ─────────────────────────────────────────────────

    /// <summary>
    /// Adds a new file version to this document.
    /// The version is NOT set as current until the document is Published.
    /// </summary>
    public DocumentVersion AddVersion(
        string blobPath,
        long fileSize,
        DocumentFileType fileType,
        string checksumHex,
        Guid uploadedBy,
        string? changeNote = null)
    {
        if (Status == DocumentStatus.Archived)
            throw new DomainException("Cannot add versions to an archived document.");

        if (Status == DocumentStatus.Published)
            throw new DomainException("Cannot add versions to a published document. Return to Draft first via ReturnToDraft().");

        foreach (var v in _versions.Where(v => v.IsCurrentVersion))
            v.ClearCurrentFlag();

        var version = DocumentVersion.Create(
            documentId: Id,
            blobPath: blobPath,
            fileSize: fileSize,
            fileType: fileType,
            checksum: FileChecksum.FromHexString(checksumHex),
            uploadedBy: uploadedBy,
            changeNote: changeNote);

        _versions.Add(version);
        return version;
    }

    // ── Metadata updates ───────────────────────────────────────────────────

    public void UpdateMetadata(
        string? title = null,
        string? description = null,
        IEnumerable<string>? tags = null,
        Guid? updatedBy = null)
    {
        if (Status == DocumentStatus.Archived)
            throw new DomainException("Archived documents cannot be modified.");

        if (!string.IsNullOrWhiteSpace(title))
        {
            if (title.Length > 500) throw new DomainException("Title cannot exceed 500 characters.");
            Title = title.Trim();
        }

        if (description is not null) Description = description;
        if (tags is not null) Tags = tags.ToList().AsReadOnly();

        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private void EnsureStatus(DocumentStatus required, string message)
    {
        if (Status != required)
            throw new InvalidDocumentWorkflowStateException(Id, Status, required, message);
    }
}

