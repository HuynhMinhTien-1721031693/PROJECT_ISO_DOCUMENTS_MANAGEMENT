using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Events;
using IsoDoc.Domain.Exceptions;
using Xunit;

namespace IsoDoc.Domain.Tests;

/// <summary>Unit tests for <see cref="Document"/> workflow transitions (draft → review → publish).</summary>
public sealed class DocumentWorkflowTests
{
    private static readonly string ValidSha256Hex = new('a', 64);

    private static Document CreateDraftWithFileVersion()
    {
        var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dept = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var doc = Document.Create(
            "Procedure X",
            "QMS-PR-001",
            IsoStandard.ISO9001,
            DocumentCategory.Procedure,
            owner,
            departmentId: dept);
        doc.ClearDomainEvents();
        doc.AddVersion("blobs/test.pdf", 1024, DocumentFileType.Pdf, ValidSha256Hex, owner);
        return doc;
    }

    [Fact]
    public void SubmitForReview_FromDraft_WithFile_MovesToUnderReview()
    {
        var doc = CreateDraftWithFileVersion();
        var submitter = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        doc.SubmitForReview(submitter);

        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
        Assert.Equal(submitter, doc.UpdatedBy);
        Assert.Contains(doc.DomainEvents, e => e is DocumentSubmittedForReviewEvent);
        Assert.Contains(doc.DomainEvents, e => e is DocumentIndexSyncEvent);
    }

    [Fact]
    public void SubmitForReview_FromDraft_WithoutFile_Throws()
    {
        var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dept = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var doc = Document.Create(
            "Empty doc",
            "QMS-PR-002",
            IsoStandard.ISO9001,
            DocumentCategory.Procedure,
            owner,
            departmentId: dept);
        doc.ClearDomainEvents();

        var ex = Assert.Throws<DomainException>(() => doc.SubmitForReview(owner));

        Assert.Contains("no uploaded file", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubmitForReview_WhenNotDraft_ThrowsInvalidWorkflow()
    {
        var doc = CreateDraftWithFileVersion();
        doc.SubmitForReview(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        doc.ClearDomainEvents();

        var ex = Assert.Throws<InvalidDocumentWorkflowStateException>(() =>
            doc.SubmitForReview(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")));

        Assert.Equal(DocumentStatus.UnderReview, ex.CurrentStatus);
    }

    [Fact]
    public void AdvanceToFinalApproval_FromUnderReview_MovesToPendingFinalApproval()
    {
        var doc = CreateDraftWithFileVersion();
        var qa = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        doc.SubmitForReview(qa);
        doc.ClearDomainEvents();

        doc.AdvanceToFinalApproval(qa);

        Assert.Equal(DocumentStatus.PendingFinalApproval, doc.Status);
        Assert.Equal(qa, doc.UpdatedBy);
        Assert.Contains(doc.DomainEvents, e => e is DocumentIndexSyncEvent);
    }

    [Fact]
    public void AdvanceToFinalApproval_FromDraft_ThrowsInvalidWorkflow()
    {
        var doc = CreateDraftWithFileVersion();

        var ex = Assert.Throws<InvalidDocumentWorkflowStateException>(() =>
            doc.AdvanceToFinalApproval(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")));

        Assert.Equal(DocumentStatus.Draft, ex.CurrentStatus);
    }

    [Fact]
    public void Publish_FromPendingFinalApproval_BumpsMinorAndSetsPublished()
    {
        var doc = CreateDraftWithFileVersion();
        var qa = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var isoManager = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        doc.SubmitForReview(qa);
        doc.AdvanceToFinalApproval(qa);
        doc.ClearDomainEvents();

        doc.Publish(isoManager, isMajorChange: false);

        Assert.Equal(DocumentStatus.Published, doc.Status);
        Assert.Equal("1.1", doc.CurrentVersion.ToString());
        Assert.Contains(doc.DomainEvents, e => e is DocumentPublishedEvent);
        Assert.Contains(doc.DomainEvents, e => e is DocumentIndexSyncEvent);
    }

    [Fact]
    public void Publish_WithMajorChange_BumpsMajor()
    {
        var doc = CreateDraftWithFileVersion();
        var qa = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var isoManager = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        doc.SubmitForReview(qa);
        doc.AdvanceToFinalApproval(qa);
        doc.ClearDomainEvents();

        doc.Publish(isoManager, isMajorChange: true);

        Assert.Equal(DocumentStatus.Published, doc.Status);
        Assert.Equal("2.0", doc.CurrentVersion.ToString());
    }

    [Fact]
    public void FullApprovalPath_DraftThroughPublish()
    {
        var doc = CreateDraftWithFileVersion();
        var qa = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var isoManager = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        doc.SubmitForReview(qa);
        Assert.Equal(DocumentStatus.UnderReview, doc.Status);

        doc.AdvanceToFinalApproval(qa);
        Assert.Equal(DocumentStatus.PendingFinalApproval, doc.Status);

        doc.Publish(isoManager);
        Assert.Equal(DocumentStatus.Published, doc.Status);
    }

    [Fact]
    public void Reject_FromUnderReview_SetsRejected()
    {
        var doc = CreateDraftWithFileVersion();
        var qa = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        doc.SubmitForReview(qa);
        doc.ClearDomainEvents();

        doc.Reject(qa, "Missing annex A");

        Assert.Equal(DocumentStatus.Rejected, doc.Status);
        Assert.Contains(doc.DomainEvents, e => e is DocumentRejectedEvent);
    }

    [Fact]
    public void ReturnToDraft_FromRejected_AllowsResubmit()
    {
        var doc = CreateDraftWithFileVersion();
        var qa = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        doc.SubmitForReview(qa);
        doc.Reject(qa, "Fix typos");
        doc.ClearDomainEvents();

        doc.ReturnToDraft(owner);

        Assert.Equal(DocumentStatus.Draft, doc.Status);
        doc.AddVersion("blobs/v2.pdf", 2048, DocumentFileType.Pdf, new string('b', 64), owner);
        doc.SubmitForReview(owner);
        Assert.Equal(DocumentStatus.UnderReview, doc.Status);
    }
}
