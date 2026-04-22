using FluentValidation;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.GetPendingApprovals;

public sealed record GetPendingApprovalsQuery(
    Guid ApproverId,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedList<PendingApprovalDto>>>;

public sealed class GetPendingApprovalsQueryValidator : AbstractValidator<GetPendingApprovalsQuery>
{
    public GetPendingApprovalsQueryValidator()
    {
        RuleFor(x => x.ApproverId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetPendingApprovalsQueryHandler
    : IRequestHandler<GetPendingApprovalsQuery, Result<PagedList<PendingApprovalDto>>>
{
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly IDocumentRepository _documents;

    public GetPendingApprovalsQueryHandler(
        IApprovalWorkflowRepository workflows,
        IDocumentRepository documents)
    {
        _workflows = workflows;
        _documents = documents;
    }

    public async Task<Result<PagedList<PendingApprovalDto>>> Handle(
        GetPendingApprovalsQuery query,
        CancellationToken ct)
    {
        var pending = await _workflows.GetPendingForApproverAsync(query.ApproverId, ct);

        var items = new List<PendingApprovalDto>();
        foreach (var wf in pending)
        {
            var doc = await _documents.GetByIdAsync(wf.DocumentId, ct);
            if (doc is null)
                continue;

            items.Add(new PendingApprovalDto
            {
                WorkflowId = wf.Id,
                DocumentId = doc.Id,
                DocumentCode = doc.Code.Value,
                DocumentTitle = doc.Title,
                IsoStandard = doc.Standard.ToString(),
                SubmittedByName = string.Empty,
                SubmittedAt = wf.StartedAt,
                StepOrder = wf.CurrentStepOrder,
                TotalSteps = wf.Steps.Count
            });
        }

        items = items.OrderBy(x => x.SubmittedAt).ToList();

        var totalCount = items.Count;
        var pagedItems = items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return Result<PagedList<PendingApprovalDto>>.Success(
            new PagedList<PendingApprovalDto>(pagedItems, totalCount, query.Page, query.PageSize));
    }
}
