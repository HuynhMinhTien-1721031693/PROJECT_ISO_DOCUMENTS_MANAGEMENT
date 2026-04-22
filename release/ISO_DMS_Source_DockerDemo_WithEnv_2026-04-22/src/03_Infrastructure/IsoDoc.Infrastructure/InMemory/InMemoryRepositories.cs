using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Domain.ValueObjects;
using IsoDoc.Infrastructure.Search;

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Document> _docs = new();

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_docs.TryGetValue(id, out var doc) && !doc.IsDeleted ? doc : null);

    public Task<Document?> GetByCodeAsync(DocumentCode code, CancellationToken ct = default)
        => Task.FromResult(_docs.Values.SingleOrDefault(d => d.Code.Equals(code) && !d.IsDeleted));

    public Task<bool> ExistsAsync(DocumentCode code, CancellationToken ct = default)
        => Task.FromResult(_docs.Values.Any(d => d.Code.Equals(code) && !d.IsDeleted));

    public Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<Document>)_docs.Values.Where(d => d.Status == status).ToList());

    public Task<IReadOnlyList<Document>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<Document>)_docs.Values.Where(d => d.OwnerId == ownerId).ToList());

    public Task<IReadOnlyList<Document>> GetAllForReportingAsync(CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<Document>)_docs.Values.Where(d => !d.IsDeleted).OrderBy(d => d.Code.Value).ToList());

    public Task<SearchResult> SearchDocumentsAsync(SearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pagedQuery = query with { Page = page, PageSize = pageSize };

        var filtered = DocumentSearchFilter.Apply(_docs.Values, pagedQuery);
        var sorted = DocumentSearchFilter.ApplySort(filtered, pagedQuery).ToList();
        var total = sorted.Count;
        var pageItems = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(DocumentSearchFilter.ToSearchResult(pageItems, total, pagedQuery));
    }

    public Task AddAsync(Document document, CancellationToken ct = default)
    {
        _docs[document.Id] = document;
        return Task.CompletedTask;
    }

    public void Update(Document document)
    {
        _docs[document.Id] = document;
    }

    public void Remove(Document document)
    {
        _docs.TryRemove(document.Id, out _);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => Task.FromResult(1);
}

public sealed class InMemoryApprovalWorkflowRepository : IApprovalWorkflowRepository
{
    private readonly ConcurrentDictionary<Guid, ApprovalWorkflow> _workflows = new();

    public Task<ApprovalWorkflow?> GetByIdAsync(Guid workflowId, CancellationToken ct = default)
        => Task.FromResult(_workflows.TryGetValue(workflowId, out var w) ? w : null);

    public Task<ApprovalWorkflow?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        var w = _workflows.Values
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        return Task.FromResult(w);
    }

    public Task<ApprovalWorkflow?> GetActiveWorkflowAsync(Guid documentId, CancellationToken ct = default)
    {
        var w = _workflows.Values
            .Where(x => x.DocumentId == documentId && !x.IsCompleted)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        return Task.FromResult(w);
    }

    public Task<ApprovalWorkflow?> GetLatestWorkflowForDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var w = _workflows.Values
            .Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        return Task.FromResult(w);
    }

    public Task<IReadOnlyList<ApprovalWorkflow>> GetPendingForApproverAsync(Guid approverId, CancellationToken ct = default)
    {
        var list = _workflows.Values
            .Where(w => !w.IsCompleted && w.CurrentStep is not null && w.CurrentStep.ApproverId == approverId)
            .ToList();
        return Task.FromResult((IReadOnlyList<ApprovalWorkflow>)list);
    }

    public Task<IReadOnlyList<ApprovalWorkflow>> GetAllForReportingAsync(CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<ApprovalWorkflow>)_workflows.Values
            .OrderByDescending(w => w.StartedAt)
            .ToList());

    public Task AddAsync(ApprovalWorkflow workflow, CancellationToken ct = default)
    {
        _workflows[workflow.Id] = workflow;
        return Task.CompletedTask;
    }

    public void Update(ApprovalWorkflow workflow)
    {
        _workflows[workflow.Id] = workflow;
    }
}

