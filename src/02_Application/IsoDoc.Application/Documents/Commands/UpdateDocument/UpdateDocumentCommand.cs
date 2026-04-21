using FluentValidation;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.Commands.UpdateDocument;

[Authorize(Permission = Permissions.DocumentEdit)]
public sealed record UpdateDocumentCommand : IRequest<Result>
{
    public Guid DocumentId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public IList<string>? Tags { get; init; }
}

public sealed class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("DocumentId không được để trống.");

        RuleFor(x => x.Title)
            .MaximumLength(500).WithMessage("Tiêu đề không quá 500 ký tự.")
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage("Mô tả không quá 4000 ký tự.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 10)
            .WithMessage("Tối đa 10 tags.");

        RuleForEach(x => x.Tags!)
            .MaximumLength(50).WithMessage("Mỗi tag tối đa 50 ký tự.")
            .When(x => x.Tags is not null);
    }
}

public sealed class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, Result>
{
    private readonly IDocumentRepository _documents;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<UpdateDocumentCommandHandler> _logger;

    public UpdateDocumentCommandHandler(
        IDocumentRepository documents,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<UpdateDocumentCommandHandler> logger)
    {
        _documents = documents;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateDocumentCommand command, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (document is null)
        {
            return Result.Failure(
                $"Không tìm thấy tài liệu '{command.DocumentId}'.",
                "DOCUMENT_NOT_FOUND");
        }

        var userId = _currentUser.UserId!.Value;
        document.UpdateMetadata(
            title: command.Title,
            description: command.Description,
            tags: command.Tags,
            updatedBy: userId);

        _documents.Update(document);
        await _documents.SaveChangesAsync(ct);

        await _audit.LogAsync(
            userId: userId,
            action: "Document.UpdateMetadata",
            entityType: "Document",
            entityId: document.Id.ToString(),
            newValues: new
            {
                command.Title,
                command.Description,
                command.Tags
            },
            ct: ct);

        _logger.LogInformation(
            "Document {DocumentId} metadata updated by {UserId}",
            document.Id,
            userId);

        return Result.Success();
    }
}
