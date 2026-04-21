using AutoMapper;
using FluentValidation;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.GetWorkflowById;

public sealed record GetWorkflowByIdQuery(Guid WorkflowId) : IRequest<Result<WorkflowDetailDto>>;

public sealed class GetWorkflowByIdQueryValidator : AbstractValidator<GetWorkflowByIdQuery>
{
    public GetWorkflowByIdQueryValidator()
    {
        RuleFor(x => x.WorkflowId).NotEmpty();
    }
}

public sealed class GetWorkflowByIdQueryHandler : IRequestHandler<GetWorkflowByIdQuery, Result<WorkflowDetailDto>>
{
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly IDocumentRepository _documents;
    private readonly ICurrentUserService _currentUser;
    private readonly IUserDirectoryLookup _directory;
    private readonly IMapper _mapper;

    public GetWorkflowByIdQueryHandler(
        IApprovalWorkflowRepository workflows,
        IDocumentRepository documents,
        ICurrentUserService currentUser,
        IUserDirectoryLookup directory,
        IMapper mapper)
    {
        _workflows = workflows;
        _documents = documents;
        _currentUser = currentUser;
        _directory = directory;
        _mapper = mapper;
    }

    public async Task<Result<WorkflowDetailDto>> Handle(GetWorkflowByIdQuery query, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
            return Result<WorkflowDetailDto>.Failure("Chua dang nhap.", "UNAUTHORIZED");

        var workflow = await _workflows.GetByIdAsync(query.WorkflowId, ct);
        if (workflow is null)
            return Result<WorkflowDetailDto>.Failure("Khong tim thay workflow.", "WORKFLOW_NOT_FOUND");

        var document = await _documents.GetByIdAsync(workflow.DocumentId, ct);
        if (document is null)
            return Result<WorkflowDetailDto>.Failure("Khong tim thay tai lieu.", "DOCUMENT_NOT_FOUND");

        var isAdmin = _currentUser.Roles.Any(r =>
            string.Equals(r, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
        var isOwner = document.OwnerId == userId.Value;
        var isApprover = workflow.Steps.Any(s => s.ApproverId == userId.Value);
        if (!isAdmin && !isOwner && !isApprover)
            return Result<WorkflowDetailDto>.Failure("Khong co quyen xem workflow nay.", "FORBIDDEN");

        var wfDto = EnrichWorkflow(workflow);
        return Result<WorkflowDetailDto>.Success(new WorkflowDetailDto
        {
            DocumentId = document.Id,
            DocumentCode = document.Code.Value,
            DocumentTitle = document.Title,
            Workflow = wfDto
        });
    }

    private WorkflowStatusDto EnrichWorkflow(ApprovalWorkflow workflow)
    {
        var dto = _mapper.Map<WorkflowStatusDto>(workflow);
        var steps = workflow.Steps
            .OrderBy(s => s.StepOrder)
            .Select(s =>
            {
                var stepDto = _mapper.Map<ApprovalStepDto>(s);
                var name = _directory.TryGetDisplayName(s.ApproverId)
                    ?? _directory.TryGetEmail(s.ApproverId)
                    ?? s.ApproverId.ToString("N")[..8];
                return stepDto with { ApproverName = name };
            })
            .ToList();

        string? currentName = null;
        if (workflow.Status == WorkflowStatus.InProgress)
        {
            var current = workflow.Steps.SingleOrDefault(s =>
                s.StepOrder == workflow.CurrentStepOrder && s.Decision == WorkflowDecision.Pending);
            if (current is not null)
            {
                currentName = _directory.TryGetDisplayName(current.ApproverId)
                    ?? _directory.TryGetEmail(current.ApproverId);
            }
        }

        return dto with { Steps = steps, CurrentApproverName = currentName };
    }
}
