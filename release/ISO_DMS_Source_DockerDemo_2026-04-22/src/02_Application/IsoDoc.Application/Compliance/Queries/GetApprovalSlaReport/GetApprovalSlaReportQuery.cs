using FluentValidation;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Compliance.Queries.GetApprovalSlaReport;

[Authorize(Permission = Permissions.ComplianceReportView)]
public sealed record GetApprovalSlaReportQuery : IRequest<Result<IReadOnlyList<ApprovalSlaReportItemDto>>>
{
    public DateTime? CompletedFromUtc { get; init; }
    public DateTime? CompletedToUtc { get; init; }
}

public sealed class GetApprovalSlaReportQueryValidator : AbstractValidator<GetApprovalSlaReportQuery>
{
    public GetApprovalSlaReportQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.CompletedFromUtc is null || x.CompletedToUtc is null || x.CompletedFromUtc <= x.CompletedToUtc)
            .WithMessage("CompletedFromUtc phải trước hoặc bằng CompletedToUtc.");
    }
}

public sealed class GetApprovalSlaReportQueryHandler
    : IRequestHandler<GetApprovalSlaReportQuery, Result<IReadOnlyList<ApprovalSlaReportItemDto>>>
{
    private readonly IComplianceReportRepository _reports;

    public GetApprovalSlaReportQueryHandler(IComplianceReportRepository reports) => _reports = reports;

    public async Task<Result<IReadOnlyList<ApprovalSlaReportItemDto>>> Handle(
        GetApprovalSlaReportQuery request,
        CancellationToken ct)
    {
        var rows = await _reports.GetApprovalSlaRowsAsync(
            request.CompletedFromUtc,
            request.CompletedToUtc,
            ct);

        var dtos = rows
            .Select(r => new ApprovalSlaReportItemDto
            {
                WorkflowId = r.WorkflowId,
                DocumentId = r.DocumentId,
                DocumentCode = r.DocumentCode,
                DocumentTitle = r.DocumentTitle,
                StartedAtUtc = r.StartedAtUtc,
                CompletedAtUtc = r.CompletedAtUtc,
                WorkflowStatus = r.WorkflowStatus.ToString(),
                ClosedCycleHours = r.ClosedCycleHours,
                OpenWaitingHours = r.OpenWaitingHours
            })
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<ApprovalSlaReportItemDto>>.Success(dtos);
    }
}
