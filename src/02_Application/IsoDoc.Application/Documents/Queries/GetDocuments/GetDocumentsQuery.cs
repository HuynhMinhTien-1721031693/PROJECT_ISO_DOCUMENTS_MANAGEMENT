using FluentValidation;
using IsoDoc.Application.Common.Models;
using IsoDoc.Application.Documents.Mapping;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.GetDocuments;

/// <summary>
/// Paged document list backed by <see cref="IDocumentRepository.SearchDocumentsAsync"/> (SQL / in-memory),
/// with filtering and sorting — authoritative list for the UI.
/// </summary>
public sealed record GetDocumentsQuery : IRequest<Result<PagedList<DocumentSummaryDto>>>
{
    public string? Keyword { get; init; }
    public IsoStandard? Standard { get; init; }
    public DocumentStatus? Status { get; init; }
    public DocumentCategory? Category { get; init; }
    public Guid? OwnerId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string SortBy { get; init; } = "UpdatedAt";
    public bool SortDesc { get; init; } = true;
}

public sealed class GetDocumentsQueryValidator : AbstractValidator<GetDocumentsQuery>
{
    public GetDocumentsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page phải >= 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize phải từ 1 đến 100.");

        RuleFor(x => x.Keyword)
            .MaximumLength(200).When(x => x.Keyword is not null)
            .WithMessage("Từ khóa tìm kiếm không quá 200 ký tự.");

        RuleFor(x => x)
            .Must(x => x.FromDate is null || x.ToDate is null || x.FromDate <= x.ToDate)
            .WithMessage("FromDate phải trước hoặc bằng ToDate.");

        RuleFor(x => x.SortBy)
            .Must(s => new[] { "UpdatedAt", "CreatedAt", "Title", "DocumentCode", "Code", "Status" }.Contains(s))
            .WithMessage("SortBy không hợp lệ.");
    }
}

public sealed class GetDocumentsQueryHandler
    : IRequestHandler<GetDocumentsQuery, Result<PagedList<DocumentSummaryDto>>>
{
    private readonly IDocumentRepository _documents;

    public GetDocumentsQueryHandler(IDocumentRepository documents) => _documents = documents;

    public async Task<Result<PagedList<DocumentSummaryDto>>> Handle(
        GetDocumentsQuery query,
        CancellationToken ct)
    {
        var searchQuery = new SearchQuery(
            Keyword: query.Keyword,
            Standard: query.Standard,
            Status: query.Status,
            Category: query.Category,
            OwnerId: query.OwnerId,
            FromDate: query.FromDate,
            ToDate: query.ToDate,
            Page: query.Page,
            PageSize: query.PageSize,
            SortBy: query.SortBy,
            SortDesc: query.SortDesc);

        var result = await _documents.SearchDocumentsAsync(searchQuery, ct);

        var items = result.Hits
            .Select(DocumentSummaryMapping.FromSearchHit)
            .ToList();

        var paged = new PagedList<DocumentSummaryDto>(
            items, result.TotalCount, result.Page, result.PageSize);

        return Result<PagedList<DocumentSummaryDto>>.Success(paged);
    }
}
