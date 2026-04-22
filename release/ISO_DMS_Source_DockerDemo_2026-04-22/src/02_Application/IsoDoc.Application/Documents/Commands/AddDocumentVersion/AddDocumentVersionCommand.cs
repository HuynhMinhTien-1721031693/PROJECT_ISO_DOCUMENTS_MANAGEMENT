using FluentValidation;
using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.Commands.AddDocumentVersion;

[Authorize(Permission = Permissions.DocumentUpload)]
public sealed record AddDocumentVersionCommand : IRequest<Result<Guid>>
{
    public Guid DocumentId { get; init; }

    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumHex { get; init; } = string.Empty;
    public string? ChangeNote { get; init; }
}

public sealed class AddDocumentVersionCommandValidator : AbstractValidator<AddDocumentVersionCommand>
{
    public AddDocumentVersionCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("DocumentId không được để trống.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Tên file không được để trống.");

        RuleFor(x => x.ContentType)
            .Must(ct => DocumentFileUploadRules.AllowedContentTypes.Contains(ct))
            .WithMessage("Chỉ chấp nhận file PDF, DOCX hoặc XLSX.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File không được rỗng.")
            .LessThanOrEqualTo(DocumentFileUploadRules.MaxFileSizeBytes)
            .WithMessage($"Dung lượng file không vượt quá {DocumentFileUploadRules.MaxFileSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.ChecksumHex)
            .NotEmpty().WithMessage("Checksum SHA-256 bắt buộc phải có.")
            .Length(64).WithMessage("Checksum phải là chuỗi hex 64 ký tự (SHA-256).")
            .Must(h => h.All(c => char.IsAsciiHexDigit(c)))
            .WithMessage("Checksum phải là chuỗi hex hợp lệ.");
    }
}

public sealed class AddDocumentVersionCommandHandler : IRequestHandler<AddDocumentVersionCommand, Result<Guid>>
{
    private readonly IDocumentRepository _documents;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ISearchService _search;
    private readonly ILogger<AddDocumentVersionCommandHandler> _logger;

    public AddDocumentVersionCommandHandler(
        IDocumentRepository documents,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        IAuditService audit,
        ISearchService search,
        ILogger<AddDocumentVersionCommandHandler> logger)
    {
        _documents = documents;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _audit = audit;
        _search = search;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(AddDocumentVersionCommand command, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (document is null)
        {
            return Result<Guid>.Failure(
                $"Không tìm thấy tài liệu '{command.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var ownerId = _currentUser.UserId!.Value;

        string blobPath;
        try
        {
            if (command.FileStream.CanSeek)
                command.FileStream.Position = 0;

            blobPath = await _fileStorage.UploadAsync(
                fileStream: command.FileStream,
                fileName: command.FileName,
                contentType: command.ContentType,
                documentId: document.Id,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload failed for new version of document {DocumentId}", command.DocumentId);
            return Result<Guid>.Failure(
                "Lỗi khi tải file lên. Vui lòng thử lại.",
                "FILE_UPLOAD_FAILED");
        }

        var fileType = command.ContentType switch
        {
            "application/pdf" => DocumentFileType.Pdf,
            var ct2 when ct2.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase) => DocumentFileType.Docx,
            var ct2 when ct2.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase) => DocumentFileType.Xlsx,
            _ => DocumentFileType.Pdf
        };

        DocumentVersion version;
        try
        {
            version = document.AddVersion(
                blobPath: blobPath,
                fileSize: command.FileSize,
                fileType: fileType,
                checksumHex: command.ChecksumHex.Trim().ToUpperInvariant(),
                uploadedBy: ownerId,
                changeNote: command.ChangeNote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddVersion failed for document {DocumentId}", command.DocumentId);
            return Result<Guid>.Failure(ex.Message, "VERSION_ADD_FAILED");
        }

        _documents.Update(document);
        await _documents.SaveChangesAsync(ct);

        try
        {
            await _search.UpdateDocumentIndexAsync(document, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search index update failed after new version for document {DocumentId}", document.Id);
        }

        await _audit.LogAsync(
            userId: ownerId,
            action: "Document.AddVersion",
            entityType: "Document",
            entityId: document.Id.ToString(),
            newValues: new { document.Id, VersionId = version.Id, command.FileName },
            ct: ct);

        _logger.LogInformation("New file version {VersionId} for document {DocumentId}", version.Id, document.Id);

        return Result<Guid>.Success(version.Id);
    }
}
