using AutoMapper;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(Guid DocumentId, bool IncludeVersions = true)
    : IRequest<Result<DocumentDto>>;

public sealed class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, Result<DocumentDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly IUserDirectoryLookup _directory;
    private readonly IMapper _mapper;

    public GetDocumentByIdQueryHandler(
        IDocumentRepository documents,
        IApprovalWorkflowRepository workflows,
        IUserDirectoryLookup directory,
        IMapper mapper)
    {
        _documents = documents;
        _workflows = workflows;
        _directory = directory;
        _mapper = mapper;
    }

    public async Task<Result<DocumentDto>> Handle(GetDocumentByIdQuery query, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(query.DocumentId, ct);
        if (document is null)
        {
            return Result<DocumentDto>.Failure(
                $"Không tìm thấy tài liệu '{query.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var dto = _mapper.Map<DocumentDto>(document);

        if (query.IncludeVersions)
        {
            var versions = document.Versions
                .OrderByDescending(v => v.UploadedAt)
                .Select(v => _mapper.Map<DocumentVersionDto>(v))
                .ToList();
            dto = dto with { Versions = versions };
        }
        else
        {
            dto = dto with { Versions = Array.Empty<DocumentVersionDto>() };
        }

        var active = await _workflows.GetActiveWorkflowAsync(document.Id, ct);
        var workflow = active ?? await _workflows.GetLatestWorkflowForDocumentAsync(document.Id, ct);
        if (workflow is not null)
            dto = dto with { ActiveWorkflow = EnrichWorkflow(workflow) };

        return Result<DocumentDto>.Success(dto);
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
