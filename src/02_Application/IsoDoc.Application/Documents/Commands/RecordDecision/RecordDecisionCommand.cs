using FluentValidation;
using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Common;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Exceptions;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.Commands.RecordDecision;

[Authorize(Permission = Permissions.DocumentApprove)]
public sealed record RecordDecisionCommand : IRequest<Result>
{
    public Guid WorkflowId { get; init; }
    public WorkflowDecision Decision { get; init; }
    public string? Comment { get; init; }
}

public sealed class RecordDecisionCommandValidator : AbstractValidator<RecordDecisionCommand>
{
    public RecordDecisionCommandValidator()
    {
        RuleFor(x => x.WorkflowId)
            .NotEmpty().WithMessage("WorkflowId không được để trống.");

        RuleFor(x => x.Decision)
            .Must(d => d is WorkflowDecision.Approved or WorkflowDecision.Rejected)
            .WithMessage("Decision phải là Approved hoặc Rejected.");

        RuleFor(x => x.Comment)
            .NotEmpty().When(x => x.Decision == WorkflowDecision.Rejected)
            .WithMessage("Bắt buộc phải nhập lý do khi từ chối.");

        RuleFor(x => x.Comment)
            .MaximumLength(2000).When(x => x.Comment is not null)
            .WithMessage("Comment không quá 2000 ký tự.");
    }
}

public sealed class RecordDecisionCommandHandler : IRequestHandler<RecordDecisionCommand, Result>
{
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly IDocumentRepository _documents;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IMediator _mediator;
    private readonly ILogger<RecordDecisionCommandHandler> _logger;

    public RecordDecisionCommandHandler(
        IApprovalWorkflowRepository workflows,
        IDocumentRepository documents,
        ICurrentUserService currentUser,
        IAuditService audit,
        IMediator mediator,
        ILogger<RecordDecisionCommandHandler> logger)
    {
        _workflows = workflows;
        _documents = documents;
        _currentUser = currentUser;
        _audit = audit;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result> Handle(RecordDecisionCommand command, CancellationToken ct)
    {
        var workflow = await _workflows.GetByIdAsync(command.WorkflowId, ct);
        if (workflow is null)
        {
            return Result.Failure("Không tìm thấy workflow.", "WORKFLOW_NOT_FOUND");
        }

        var document = await _documents.GetByIdAsync(workflow.DocumentId, ct);
        if (document is null)
        {
            return Result.Failure("Không tìm thấy tài liệu.", "DOCUMENT_NOT_FOUND");
        }

        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure("Nguoi dung chua dang nhap.", "UNAUTHORIZED");
        }

        try
        {
            workflow.RecordDecision(userId.Value, command.Decision, command.Comment);
        }
        catch (UnauthorizedWorkflowAccessException ex)
        {
            return Result.Failure(ex.Message, "NOT_ASSIGNED_APPROVER");
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message, "WORKFLOW_ACTION_FAILED");
        }

        if (workflow.Status == WorkflowStatus.Approved)
        {
            try
            {
                document.Publish(userId.Value, isMajorChange: false);
            }
            catch (DomainException ex)
            {
                return Result.Failure(ex.Message, "INVALID_DOCUMENT_STATE");
            }
        }
        else if (workflow.Status == WorkflowStatus.Rejected)
        {
            try
            {
                document.Reject(userId.Value, command.Comment ?? "No reason provided.");
            }
            catch (DomainException ex)
            {
                return Result.Failure(ex.Message, "INVALID_DOCUMENT_STATE");
            }
        }
        else if (command.Decision == WorkflowDecision.Approved && workflow.CurrentStepOrder == 2)
        {
            try
            {
                document.AdvanceToFinalApproval(userId.Value);
            }
            catch (DomainException ex)
            {
                return Result.Failure(ex.Message, "INVALID_DOCUMENT_STATE");
            }
        }

        _workflows.Update(workflow);
        _documents.Update(document);
        await _documents.SaveChangesAsync(ct);

        await DomainEventsPublisher.PublishAndClearAsync(
            _mediator,
            new BaseEntity[] { workflow, document },
            ct);

        var action = command.Decision == WorkflowDecision.Approved
            ? "Workflow.Approved"
            : "Workflow.Rejected";

        await _audit.LogAsync(userId.Value, action, "ApprovalWorkflow",
            workflow.Id.ToString(),
            newValues: new { command.Decision, command.Comment }, ct: ct);

        _logger.LogInformation(
            "Workflow {WorkflowId} step decision {Decision} by {UserId}",
            workflow.Id, command.Decision, userId.Value);

        return Result.Success();
    }
}
