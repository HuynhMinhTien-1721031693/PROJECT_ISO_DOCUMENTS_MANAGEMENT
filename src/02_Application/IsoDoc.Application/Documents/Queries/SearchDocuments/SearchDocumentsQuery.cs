using FluentValidation;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Documents.Queries.SearchDocuments;

public sealed record SearchDocumentsQuery : IRequest<Result<PagedList<DocumentSummaryDto>>>
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

public sealed class SearchDocumentsQueryValidator : AbstractValidator<SearchDocumentsQuery>
{
    public SearchDocumentsQueryValidator()
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
            .Must(s => new[] { "UpdatedAt", "CreatedAt", "Title", "DocumentCode", "Status" }.Contains(s))
            .WithMessage("SortBy không hợp lệ.");
    }
}

public sealed class SearchDocumentsQueryHandler
    : IRequestHandler<SearchDocumentsQuery, Result<PagedList<DocumentSummaryDto>>>
{
    private readonly ISearchService _searchService;

    public SearchDocumentsQueryHandler(ISearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task<Result<PagedList<DocumentSummaryDto>>> Handle(
        SearchDocumentsQuery query,
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

        var result = await _searchService.SearchAsync(searchQuery, ct);

        var items = result.Hits
            .Select(hit => new DocumentSummaryDto
            {
                Id = hit.DocumentId,
                DocumentCode = hit.DocumentCode,
                Title = hit.Title,
                Status = hit.Status,
                IsoStandard = hit.Standard,
                Category = hit.Category,
                CurrentVersion = string.Empty,
                OwnerName = string.Empty,
                UpdatedAt = hit.UpdatedAt,
                HighlightedFragments = hit.Highlights
            })
            .ToList();

        var paged = new PagedList<DocumentSummaryDto>(
            items, result.TotalCount, query.Page, query.PageSize);

        return Result<PagedList<DocumentSummaryDto>>.Success(paged);
    }
}
