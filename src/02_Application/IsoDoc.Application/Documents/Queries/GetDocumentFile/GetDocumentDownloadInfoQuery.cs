using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Configuration;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Application.Documents.Queries.GetDocumentFile;

public sealed record GetDocumentDownloadInfoQuery(Guid DocumentId, Guid? VersionId)
    : IRequest<Result<DocumentDownloadInfoDto>>;

public sealed class GetDocumentDownloadInfoQueryHandler
    : IRequestHandler<GetDocumentDownloadInfoQuery, Result<DocumentDownloadInfoDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IFileStorageService _fileStorage;
    private readonly BlobStorageOptions _blobOptions;
    private readonly ILogger<GetDocumentDownloadInfoQueryHandler> _logger;

    public GetDocumentDownloadInfoQueryHandler(
        IDocumentRepository documents,
        IFileStorageService fileStorage,
        IOptions<BlobStorageOptions> blobOptions,
        ILogger<GetDocumentDownloadInfoQueryHandler> logger)
    {
        _documents = documents;
        _fileStorage = fileStorage;
        _blobOptions = blobOptions.Value;
        _logger = logger;
    }

    public async Task<Result<DocumentDownloadInfoDto>> Handle(GetDocumentDownloadInfoQuery query, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(query.DocumentId, ct);
        if (document is null)
        {
            return Result<DocumentDownloadInfoDto>.Failure(
                $"Không tìm thấy tài liệu '{query.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var version = DocumentFileAccess.ResolveVersion(document, query.VersionId);
        if (version is null)
        {
            return Result<DocumentDownloadInfoDto>.Failure(
                "Không tìm thấy phiên bản file yêu cầu.",
                "VERSION_NOT_FOUND");
        }

        var fileName = DocumentBlobPathParser.OriginalFileName(version.BlobPath);
        var contentType = DocumentFileMime.ForFileType(version.FileType);
        var checksum = version.Checksum.Value;

        if (_fileStorage.SupportsTimeLimitedPublicUrls)
        {
            try
            {
                var expiry = TimeSpan.FromHours(Math.Clamp(_blobOptions.SasExpiryHours, 1, 24));
                var sas = await _fileStorage.GetSecureDownloadUrlAsync(version.BlobPath, expiry, ct);
                return Result<DocumentDownloadInfoDto>.Success(
                    new DocumentDownloadInfoDto(
                        Mode: "Sas",
                        SasUrl: sas,
                        SasExpiresAt: DateTimeOffset.UtcNow.Add(expiry),
                        ApiRelativeUrl: null,
                        FileName: fileName,
                        ContentType: contentType,
                        FileSize: version.FileSize,
                        ChecksumHex: checksum));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SAS URL generation failed; falling back to API file endpoint.");
            }
        }

        var versionQuery = version.Id == Guid.Empty ? string.Empty : $"?versionId={version.Id}";
        var relative = $"Documents/{document.Id}/file{versionQuery}";
        return Result<DocumentDownloadInfoDto>.Success(
            new DocumentDownloadInfoDto(
                Mode: "Api",
                SasUrl: null,
                SasExpiresAt: null,
                ApiRelativeUrl: relative,
                FileName: fileName,
                ContentType: contentType,
                FileSize: version.FileSize,
                ChecksumHex: checksum));
    }
}
