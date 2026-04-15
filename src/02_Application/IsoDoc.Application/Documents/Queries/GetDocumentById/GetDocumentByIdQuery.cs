using AutoMapper;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.GetDocumentById;

public sealed record GetDocumentByIdQuery(Guid DocumentId, bool IncludeVersions = true)
    : IRequest<Result<DocumentDto>>;

public sealed class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, Result<DocumentDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly IMapper _mapper;

    public GetDocumentByIdQueryHandler(
        IDocumentRepository documents,
        IApprovalWorkflowRepository workflows,
        IMapper mapper)
    {
        _documents = documents;
        _workflows = workflows;
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
        if (active is not null)
        {
            dto = dto with { ActiveWorkflow = _mapper.Map<WorkflowStatusDto>(active) };
        }

        return Result<DocumentDto>.Success(dto);
    }
}
