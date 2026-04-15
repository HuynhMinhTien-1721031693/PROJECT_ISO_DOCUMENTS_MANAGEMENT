using FluentValidation;
using IsoDoc.Application.Common;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.Commands.UploadDocument;

[Authorize(Permission = Permissions.DocumentUpload)]
public sealed record UploadDocumentCommand : IRequest<Result<Guid>>
{
    public string Title { get; init; } = string.Empty;
    public string DocumentCode { get; init; } = string.Empty;
    public IsoStandard Standard { get; init; }
    public DocumentCategory Category { get; init; }
    public string? Description { get; init; }
    public IList<string> Tags { get; init; } = new List<string>();

    public Stream FileStream { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string ChecksumHex { get; init; } = string.Empty;
    public string? ChangeNote { get; init; }
}

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề tài liệu không được để trống.")
            .MaximumLength(500).WithMessage("Tiêu đề không quá 500 ký tự.");

        RuleFor(x => x.DocumentCode)
            .NotEmpty().WithMessage("Mã tài liệu không được để trống.")
            .Must(code =>
            {
                try
                {
                    _ = DocumentCode.Create(code.Trim());
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("Mã tài liệu phải theo định dạng: PREFIX-TYPE-NNN (ví dụ: QMS-PR-001).");

        RuleFor(x => x.Standard)
            .IsInEnum().WithMessage("Tiêu chuẩn ISO không hợp lệ.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Phân loại tài liệu không hợp lệ.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Tên file không được để trống.");

        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Chỉ chấp nhận file PDF, DOCX hoặc XLSX.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File không được rỗng.")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"Dung lượng file không vượt quá {MaxFileSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.ChecksumHex)
            .NotEmpty().WithMessage("Checksum SHA-256 bắt buộc phải có.")
            .Length(64).WithMessage("Checksum phải là chuỗi hex 64 ký tự (SHA-256).")
            .Must(h => h.All(c => char.IsAsciiHexDigit(c)))
            .WithMessage("Checksum phải là chuỗi hex hợp lệ.");

        RuleFor(x => x.Tags)
            .Must(tags => tags.Count <= 10).WithMessage("Tối đa 10 tags.");

        RuleForEach(x => x.Tags)
            .MaximumLength(50).WithMessage("Mỗi tag tối đa 50 ký tự.");
    }
}

public sealed class UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, Result<Guid>>
{
    private readonly IDocumentRepository _documents;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly IMediator _mediator;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IDocumentRepository documents,
        IFileStorageService fileStorage,
        ICurrentUserService currentUser,
        IAuditService audit,
        IMediator mediator,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _documents = documents;
        _fileStorage = fileStorage;
        _currentUser = currentUser;
        _audit = audit;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(UploadDocumentCommand command, CancellationToken ct)
    {
        DocumentCode codeVo;
        try
        {
            codeVo = DocumentCode.Create(command.DocumentCode.Trim());
        }
        catch (ArgumentException ex)
        {
            return Result<Guid>.Failure(ex.Message, "INVALID_DOCUMENT_CODE");
        }

        if (await _documents.ExistsAsync(codeVo, ct))
        {
            return Result<Guid>.Failure(
                $"Mã tài liệu '{codeVo.Value}' đã tồn tại trong hệ thống.",
                "DOCUMENT_CODE_DUPLICATE");
        }

        var ownerId = _currentUser.UserId!.Value;
        var departmentId = _currentUser.DepartmentId;
        if (departmentId == Guid.Empty)
        {
            return Result<Guid>.Failure("Thiếu thông tin phòng ban người dùng.", "DEPARTMENT_REQUIRED");
        }

        var document = Document.Create(
            title: command.Title,
            code: codeVo.Value,
            standard: command.Standard,
            category: command.Category,
            ownerId: ownerId,
            departmentId: departmentId,
            description: command.Description,
            tags: command.Tags);

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
            _logger.LogError(ex, "File upload failed for document {Code}", codeVo.Value);
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

        try
        {
            document.AddVersion(
                blobPath: blobPath,
                fileSize: command.FileSize,
                fileType: fileType,
                checksumHex: command.ChecksumHex.Trim().ToUpperInvariant(),
                uploadedBy: ownerId,
                changeNote: command.ChangeNote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddVersion failed for document {Code}", codeVo.Value);
            return Result<Guid>.Failure(ex.Message, "VERSION_ADD_FAILED");
        }

        await _documents.AddAsync(document, ct);
        await _documents.SaveChangesAsync(ct);
        await DomainEventsPublisher.PublishAndClearAsync(_mediator, document, ct);

        await _audit.LogAsync(
            userId: ownerId,
            action: "Document.Upload",
            entityType: "Document",
            entityId: document.Id.ToString(),
            newValues: new { document.Id, document.Code, command.FileName },
            ct: ct);

        _logger.LogInformation("Document created: {Code} by user {UserId}", document.Code, ownerId);

        return Result<Guid>.Success(document.Id);
    }
}
