using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Domain.ValueObjects;
using IsoDoc.Infrastructure.Search;
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
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<Document?> GetByCodeAsync(DocumentCode code, CancellationToken ct = default)
        => await _db.Documents
            .Include("_versions")
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Code == code && !d.IsDeleted, ct);

    public Task<bool> ExistsAsync(DocumentCode code, CancellationToken ct = default)
        => _db.Documents.AnyAsync(d => d.Code == code && !d.IsDeleted, ct);

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

    public async Task<IReadOnlyList<Document>> GetAllForReportingAsync(CancellationToken ct = default)
        => await _db.Documents
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.Code)
            .ToListAsync(ct);

    public async Task<SearchResult> SearchDocumentsAsync(SearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pagedQuery = query with { Page = page, PageSize = pageSize };

        var filtered = DocumentSearchFilter.Apply(_db.Documents.AsNoTracking(), pagedQuery);
        var total = await filtered.CountAsync(ct);
        var ordered = DocumentSearchFilter.ApplySort(filtered, pagedQuery);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return DocumentSearchFilter.ToSearchResult(items, total, pagedQuery);
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
        => await _db.Documents.AddAsync(document, ct);

    public void Update(Document document) => _db.Documents.Update(document);

    public void Remove(Document document) => _db.Documents.Remove(document);

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
            .Where(w => w.DocumentId == documentId)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<ApprovalWorkflow?> GetActiveWorkflowAsync(Guid documentId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .Where(w => w.DocumentId == documentId && w.Status == WorkflowStatus.InProgress)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<ApprovalWorkflow?> GetLatestWorkflowForDocumentAsync(Guid documentId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .Where(w => w.DocumentId == documentId)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ApprovalWorkflow>> GetPendingForApproverAsync(Guid approverId, CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .Where(w => w.Status == WorkflowStatus.InProgress
                     && _db.ApprovalSteps.Any(s => s.WorkflowId == w.Id
                                               && s.ApproverId == approverId
                                               && s.Decision == WorkflowDecision.Pending
                                               && s.StepOrder == w.CurrentStepOrder))
            .OrderBy(w => w.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ApprovalWorkflow>> GetAllForReportingAsync(CancellationToken ct = default)
        => await _db.ApprovalWorkflows
            .Include("_steps")
            .AsSplitQuery()
            .OrderByDescending(w => w.StartedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ApprovalWorkflow workflow, CancellationToken ct = default)
        => await _db.ApprovalWorkflows.AddAsync(workflow, ct);

    public void Update(ApprovalWorkflow workflow) => _db.ApprovalWorkflows.Update(workflow);
}

public sealed class UserNotificationRepository : IUserNotificationRepository
{
    private readonly AppDbContext _db;

    public UserNotificationRepository(AppDbContext db) => _db = db;

    public async Task<UserNotification> AddAsync(UserNotification notification, CancellationToken ct = default)
    {
        await _db.Set<UserNotification>().AddAsync(notification, ct);
        await _db.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> GetPageForUserAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Set<UserNotification>().AsQueryable().Where(n => n.UserId == userId);
        if (unreadOnly)
            q = q.Where(n => !n.IsRead);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<UserNotification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _db.Set<UserNotification>().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => _db.Set<UserNotification>().CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var n = await _db.Set<UserNotification>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null)
            return;
        n.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Set<UserNotification>()
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
