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

namespace IsoDoc.Infrastructure.InMemory;

public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly ConcurrentDictionary<Guid, Document> _docs = new();

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_docs.TryGetValue(id, out var doc) ? doc : null);

    public Task<Document?> GetByCodeAsync(DocumentCode code, CancellationToken ct = default)
        => Task.FromResult(_docs.Values.SingleOrDefault(d => d.Code.Equals(code)));

    public Task<bool> ExistsAsync(DocumentCode code, CancellationToken ct = default)
        => Task.FromResult(_docs.Values.Any(d => d.Code.Equals(code)));

    public Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<Document>)_docs.Values.Where(d => d.Status == status).ToList());

    public Task<IReadOnlyList<Document>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<Document>)_docs.Values.Where(d => d.OwnerId == ownerId).ToList());

    public Task AddAsync(Document document, CancellationToken ct = default)
    {
        _docs[document.Id] = document;
        return Task.CompletedTask;
    }

    public void Update(Document document)
    {
        _docs[document.Id] = document;
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
        => Task.FromResult(_workflows.Values.SingleOrDefault(w => w.DocumentId == documentId));

    public Task<ApprovalWorkflow?> GetActiveWorkflowAsync(Guid documentId, CancellationToken ct = default)
        => Task.FromResult(_workflows.Values.SingleOrDefault(w => w.DocumentId == documentId && !w.IsCompleted));

    public Task<IReadOnlyList<ApprovalWorkflow>> GetPendingForApproverAsync(Guid approverId, CancellationToken ct = default)
    {
        var list = _workflows.Values
            .Where(w => !w.IsCompleted && w.CurrentStep is not null && w.CurrentStep.ApproverId == approverId)
            .ToList();
        return Task.FromResult((IReadOnlyList<ApprovalWorkflow>)list);
    }

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

