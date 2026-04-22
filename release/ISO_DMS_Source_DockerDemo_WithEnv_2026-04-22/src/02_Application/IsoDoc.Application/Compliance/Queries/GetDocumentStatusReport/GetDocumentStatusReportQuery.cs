using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Compliance.Queries.GetDocumentStatusReport;

[Authorize(Permission = Permissions.ComplianceReportView)]
public sealed record GetDocumentStatusReportQuery : IRequest<Result<IReadOnlyList<DocumentStatusReportItemDto>>>;

public sealed class GetDocumentStatusReportQueryHandler
    : IRequestHandler<GetDocumentStatusReportQuery, Result<IReadOnlyList<DocumentStatusReportItemDto>>>
{
    private readonly IComplianceReportRepository _reports;

    public GetDocumentStatusReportQueryHandler(IComplianceReportRepository reports) => _reports = reports;

    public async Task<Result<IReadOnlyList<DocumentStatusReportItemDto>>> Handle(
        GetDocumentStatusReportQuery _,
        CancellationToken ct)
    {
        var rows = await _reports.GetDocumentCountsByStatusAsync(ct);
        var byStatus = rows.ToDictionary(r => r.Status, r => r.Count);

        var ordered = Enum.GetValues<DocumentStatus>()
            .Select(s => new DocumentStatusReportItemDto
            {
                Status = s.ToString(),
                Count = byStatus.TryGetValue(s, out var c) ? c : 0
            })
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<DocumentStatusReportItemDto>>.Success(ordered);
    }
}
