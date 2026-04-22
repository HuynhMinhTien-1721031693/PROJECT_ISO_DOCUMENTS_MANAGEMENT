using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Application.Documents.Mapping;

public static class DocumentSummaryMapping
{
    public static DocumentSummaryDto FromSearchHit(SearchHit hit) => new()
    {
        Id = hit.DocumentId,
        DocumentCode = hit.DocumentCode,
        Title = hit.Title,
        Status = hit.Status,
        Category = hit.Category,
        IsoStandard = hit.Standard,
        CurrentVersion = hit.CurrentVersion,
        OwnerName = string.Empty,
        UpdatedAt = hit.UpdatedAt,
        HighlightedFragments = hit.Highlights
    };
}
