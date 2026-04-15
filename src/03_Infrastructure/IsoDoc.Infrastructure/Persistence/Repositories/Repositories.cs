using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace IsoDoc.Infrastructure.Persistence.Repositories;

public sealed class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    public DocumentRepository(AppDbContext db) => _db = db;

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Documents
            .Include("_versions")
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Document?> GetByCodeAsync(DocumentCode code, CancellationToken ct = default)
        => await _db.Documents
            .Include("_versions")
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Code == code, ct);

    public Task<bool> ExistsAsync(DocumentCode code, CancellationToken ct = default)
        => _db.Documents.AnyAsync(d => d.Code == code, ct);

    public async Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken ct = default)
        => await _db.Documents
            .Where(d => d.Status == status)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Document>> GetByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        => await _db.Documents
            .Where(d => d.OwnerId == ownerId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Document document, CancellationToken ct = default)
        => await _db.Documents.AddAsync(document, ct);

    public void Update(Document document) => _db.Documents.Update(document);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class ApprovalWorkflowRepository : IApprovalWorkflowRepository
{
    private readonly AppDbContext _db;

    public ApprovalWorkflowRepository(AppDbContext db) => _db = db;

    public async Task<ApprovalWorkflow?> GetByIdAsync(Guid workflowId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct);

    public async Task<ApprovalWorkflow?> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.DocumentId == documentId, ct);

    public async Task<ApprovalWorkflow?> GetActiveWorkflowAsync(Guid documentId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .Where(w => w.DocumentId == documentId && w.Status == WorkflowStatus.InProgress)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ApprovalWorkflow>> GetPendingForApproverAsync(Guid approverId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .Where(w => w.Status == WorkflowStatus.InProgress
                     && w.Steps.Any(s => s.ApproverId == approverId
                                      && s.Decision == WorkflowDecision.Pending
                                      && s.StepOrder == w.CurrentStepOrder))
            .OrderBy(w => w.StartedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ApprovalWorkflow workflow, CancellationToken ct = default)
        => await _db.ApprovalWorkflows.AddAsync(workflow, ct);

    public void Update(ApprovalWorkflow workflow) => _db.ApprovalWorkflows.Update(workflow);
}
