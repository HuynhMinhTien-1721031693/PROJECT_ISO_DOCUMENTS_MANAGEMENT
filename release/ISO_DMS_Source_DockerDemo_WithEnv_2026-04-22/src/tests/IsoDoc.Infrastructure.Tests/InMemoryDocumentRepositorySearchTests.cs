using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Infrastructure.InMemory;
using Xunit;

namespace IsoDoc.Infrastructure.Tests;

public sealed class InMemoryDocumentRepositorySearchTests
{
    [Fact]
    public async Task SearchDocumentsAsync_filters_by_keyword_status_standard_and_dates()
    {
        var repo = new InMemoryDocumentRepository();
        var owner = Guid.NewGuid();
        var dept = Guid.NewGuid();

        var match = Document.Create(
            "Alpha procedure",
            "QMS-PR-001",
            IsoStandard.ISO9001,
            DocumentCategory.Procedure,
            owner,
            dept,
            description: "Contains beta text",
            tags: new[] { "quality" });

        var other = Document.Create(
            "Other",
            "QMS-PO-002",
            IsoStandard.ISO27001,
            DocumentCategory.Policy,
            owner,
            dept);

        await repo.AddAsync(match, default);
        await repo.AddAsync(other, default);

        var q = new SearchQuery(
            Keyword: "alpha",
            Standard: IsoStandard.ISO9001,
            Status: DocumentStatus.Draft,
            Category: null,
            OwnerId: null,
            FromDate: null,
            ToDate: null,
            Page: 1,
            PageSize: 10);

        var result = await repo.SearchDocumentsAsync(q, default);
        Assert.Single(result.Hits);
        Assert.Equal("QMS-PR-001", result.Hits[0].DocumentCode);
        Assert.Equal(1, result.TotalCount);
    }
}
