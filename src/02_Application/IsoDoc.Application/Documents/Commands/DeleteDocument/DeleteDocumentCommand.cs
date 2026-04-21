using FluentValidation;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.Commands.DeleteDocument;

[Authorize(Permission = Permissions.DocumentDelete)]
public sealed record DeleteDocumentCommand(Guid DocumentId) : IRequest<Result>;

public sealed class DeleteDocumentCommandValidator : AbstractValidator<DeleteDocumentCommand>
{
    public DeleteDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("DocumentId không được để trống.");
    }
}

public sealed class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, Result>
{
    private readonly IDocumentRepository _documents;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _fileStorage;
    private readonly ISearchService _search;
    private readonly IAuditService _audit;
    private readonly ILogger<DeleteDocumentCommandHandler> _logger;

    public DeleteDocumentCommandHandler(
        IDocumentRepository documents,
        ICurrentUserService currentUser,
        IFileStorageService fileStorage,
        ISearchService search,
        IAuditService audit,
        ILogger<DeleteDocumentCommandHandler> logger)
    {
        _documents = documents;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
        _search = search;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteDocumentCommand command, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (document is null)
        {
            return Result.Failure(
                $"Không tìm thấy tài liệu '{command.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var userId = _currentUser.UserId!.Value;

        foreach (var version in document.Versions)
        {
            await _fileStorage.DeleteAsync(version.BlobPath, ct);
        }

        document.SoftDelete(userId);
        _documents.Update(document);
        await _documents.SaveChangesAsync(ct);

        await _search.RemoveDocumentAsync(document.Id, ct);

        await _audit.LogAsync(
            userId: userId,
            action: "Document.Delete",
            entityType: "Document",
            entityId: document.Id.ToString(),
            oldValues: new { document.Id, document.Code, document.Title },
            ct: ct);

        _logger.LogInformation(
            "Document {DocumentId} deleted by {UserId}",
            document.Id,
            userId);

        return Result.Success();
    }
}
