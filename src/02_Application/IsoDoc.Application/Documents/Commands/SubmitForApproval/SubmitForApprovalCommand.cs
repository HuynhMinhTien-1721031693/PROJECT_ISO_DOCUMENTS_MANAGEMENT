using FluentValidation;
using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Exceptions;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.Commands.SubmitForApproval;

[Authorize(Permission = Permissions.DocumentSubmit)]
public sealed record SubmitForApprovalCommand(Guid DocumentId) : IRequest<Result<Guid>>;

public sealed class SubmitForApprovalCommandValidator : AbstractValidator<SubmitForApprovalCommand>
{
    public SubmitForApprovalCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("DocumentId không được để trống.");
    }
}

public sealed class SubmitForApprovalCommandHandler : IRequestHandler<SubmitForApprovalCommand, Result<Guid>>
{
    private readonly IDocumentRepository _documents;
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IApproverResolverService _approverResolver;
    private readonly IMediator _mediator;
    private readonly ILogger<SubmitForApprovalCommandHandler> _logger;

    public SubmitForApprovalCommandHandler(
        IDocumentRepository documents,
        IApprovalWorkflowRepository workflows,
        ICurrentUserService currentUser,
        IAuditService audit,
        IApproverResolverService approverResolver,
        IMediator mediator,
        ILogger<SubmitForApprovalCommandHandler> logger)
    {
        _documents = documents;
        _workflows = workflows;
        _currentUser = currentUser;
        _audit = audit;
        _approverResolver = approverResolver;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(SubmitForApprovalCommand command, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (document is null)
        {
            return Result<Guid>.Failure(
                $"Không tìm thấy tài liệu '{command.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var userId = _currentUser.UserId!.Value;

        if (document.OwnerId != userId)
        {
            return Result<Guid>.Failure(
                "Chỉ chủ sở hữu tài liệu mới có thể gửi phê duyệt.",
                "UNAUTHORIZED");
        }

        var active = await _workflows.GetActiveWorkflowAsync(document.Id, ct);
        if (active is not null)
        {
            return Result<Guid>.Failure(
                "Tài liệu đã có workflow phê duyệt đang hoạt động.",
                "WORKFLOW_ALREADY_ACTIVE");
        }

        var (step1ApproverId, step2ApproverId) =
            await _approverResolver.ResolveAsync(document.Standard, ct);

        try
        {
            document.SubmitForReview(userId);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ex.Message, "INVALID_DOCUMENT_STATE");
        }

        var latestVersion = document.Versions.OrderByDescending(v => v.UploadedAt).FirstOrDefault();
        if (latestVersion is null)
        {
            return Result<Guid>.Failure(
                "Tài liệu chưa có phiên bản file để gửi phê duyệt.",
                "NO_VERSION");
        }

        var workflow = ApprovalWorkflow.Create(
            documentId: document.Id,
            versionId: latestVersion.Id,
            step1ApproverId: step1ApproverId,
            step2ApproverId: step2ApproverId);

        await _workflows.AddAsync(workflow, ct);
        _documents.Update(document);
        await _documents.SaveChangesAsync(ct);

        await DomainEventsPublisher.PublishAndClearAsync(_mediator, document, ct);

        await _audit.LogAsync(userId, "Document.SubmitForApproval",
            "Document", document.Id.ToString(), ct: ct);

        _logger.LogInformation(
            "Document {Code} submitted for approval by {UserId}", document.Code, userId);

        return Result<Guid>.Success(workflow.Id);
    }
}
