using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.GetDocumentFile;

public sealed record GetDocumentFileMetadataQuery(Guid DocumentId, Guid? VersionId)
    : IRequest<Result<DocumentFileMetadataDto>>;

public sealed class GetDocumentFileMetadataQueryHandler
    : IRequestHandler<GetDocumentFileMetadataQuery, Result<DocumentFileMetadataDto>>
{
    private readonly IDocumentRepository _documents;

    public GetDocumentFileMetadataQueryHandler(IDocumentRepository documents) => _documents = documents;

    public async Task<Result<DocumentFileMetadataDto>> Handle(GetDocumentFileMetadataQuery query, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(query.DocumentId, ct);
        if (document is null)
        {
            return Result<DocumentFileMetadataDto>.Failure(
                $"Không tìm thấy tài liệu '{query.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var version = DocumentFileAccess.ResolveVersion(document, query.VersionId);
        if (version is null)
        {
            return Result<DocumentFileMetadataDto>.Failure(
                "Không tìm thấy phiên bản file yêu cầu.",
                "VERSION_NOT_FOUND");
        }

        var fileName = DocumentBlobPathParser.OriginalFileName(version.BlobPath);
        var contentType = DocumentFileMime.ForFileType(version.FileType);

        return Result<DocumentFileMetadataDto>.Success(
            new DocumentFileMetadataDto(
                version.BlobPath,
                fileName,
                contentType,
                version.FileSize,
                version.Checksum.Value));
    }
}
