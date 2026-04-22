using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.Search;

/// <summary>
/// Shared relational search filters (Elasticsearch fallback and in-memory store).
/// </summary>
internal static class DocumentSearchFilter
{
    public static IQueryable<Document> Apply(IQueryable<Document> query, SearchQuery search)
    {
        var q = query.Where(d => !d.IsDeleted);

        if (search.Standard.HasValue)
            q = q.Where(d => d.Standard == search.Standard.Value);

        if (search.Status.HasValue)
            q = q.Where(d => d.Status == search.Status.Value);

        if (search.Category.HasValue)
            q = q.Where(d => d.Category == search.Category.Value);

        if (search.OwnerId.HasValue)
            q = q.Where(d => d.OwnerId == search.OwnerId.Value);

        if (search.FromDate.HasValue)
            q = q.Where(d => d.UpdatedAt >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            q = q.Where(d => d.UpdatedAt <= search.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var kw = search.Keyword.Trim();
            q = q.Where(d =>
                d.Title.Contains(kw)
                || d.Code.Value.Contains(kw)
                || (d.Description != null && d.Description.Contains(kw))
                || d.Tags.Any(t => t.Contains(kw)));
        }

        return q;
    }

    public static IEnumerable<Document> Apply(IEnumerable<Document> source, SearchQuery search)
    {
        var q = source.Where(d => !d.IsDeleted);

        if (search.Standard.HasValue)
            q = q.Where(d => d.Standard == search.Standard.Value);

        if (search.Status.HasValue)
            q = q.Where(d => d.Status == search.Status.Value);

        if (search.Category.HasValue)
            q = q.Where(d => d.Category == search.Category.Value);

        if (search.OwnerId.HasValue)
            q = q.Where(d => d.OwnerId == search.OwnerId.Value);

        if (search.FromDate.HasValue)
            q = q.Where(d => d.UpdatedAt >= search.FromDate.Value);

        if (search.ToDate.HasValue)
            q = q.Where(d => d.UpdatedAt <= search.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var kw = search.Keyword.Trim();
            q = q.Where(d =>
                d.Title.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || d.Code.Value.Contains(kw, StringComparison.OrdinalIgnoreCase)
                || (d.Description != null && d.Description.Contains(kw, StringComparison.OrdinalIgnoreCase))
                || d.Tags.Any(t => t.Contains(kw, StringComparison.OrdinalIgnoreCase)));
        }

        return q;
    }

    public static IOrderedQueryable<Document> ApplySort(IQueryable<Document> query, SearchQuery search)
    {
        return search.SortBy switch
        {
            "Title" => search.SortDesc
                ? query.OrderByDescending(d => d.Title)
                : query.OrderBy(d => d.Title),
            "DocumentCode" or "Code" => search.SortDesc
                ? query.OrderByDescending(d => d.Code.Value)
                : query.OrderBy(d => d.Code.Value),
            "Status" => search.SortDesc
                ? query.OrderByDescending(d => d.Status)
                : query.OrderBy(d => d.Status),
            "CreatedAt" => search.SortDesc
                ? query.OrderByDescending(d => d.CreatedAt)
                : query.OrderBy(d => d.CreatedAt),
            _ => search.SortDesc
                ? query.OrderByDescending(d => d.UpdatedAt)
                : query.OrderBy(d => d.UpdatedAt),
        };
    }

    public static IEnumerable<Document> ApplySort(IEnumerable<Document> source, SearchQuery search)
    {
        return search.SortBy switch
        {
            "Title" => search.SortDesc
                ? source.OrderByDescending(d => d.Title)
                : source.OrderBy(d => d.Title),
            "DocumentCode" or "Code" => search.SortDesc
                ? source.OrderByDescending(d => d.Code.Value)
                : source.OrderBy(d => d.Code.Value),
            "Status" => search.SortDesc
                ? source.OrderByDescending(d => d.Status)
                : source.OrderBy(d => d.Status),
            "CreatedAt" => search.SortDesc
                ? source.OrderByDescending(d => d.CreatedAt)
                : source.OrderBy(d => d.CreatedAt),
            _ => search.SortDesc
                ? source.OrderByDescending(d => d.UpdatedAt)
                : source.OrderBy(d => d.UpdatedAt),
        };
    }

    public static SearchResult ToSearchResult(IReadOnlyList<Document> page, int total, SearchQuery search)
    {
        var hits = page
            .Select(d => new SearchHit(
                d.Id,
                d.Code.Value,
                d.Title,
                d.Status.ToString(),
                d.Category.ToString(),
                d.Standard.ToString(),
                d.UpdatedAt,
                d.CurrentVersion.ToString(),
                1d,
                Array.Empty<string>()))
            .ToList();

        return new SearchResult(hits, total, search.Page, search.PageSize);
    }
}
