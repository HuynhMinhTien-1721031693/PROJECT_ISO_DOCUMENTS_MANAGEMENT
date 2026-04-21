using FluentValidation;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Domain.Interfaces;
using MediatR;

namespace IsoDoc.Application.Audit.Queries.SearchAuditLogs;

[Authorize(Permission = Permissions.AuditLogView)]
public sealed record SearchAuditLogsQuery : IRequest<Result<PagedList<AuditLogDto>>>
{
    public Guid? UserId { get; init; }
    public string? Action { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class SearchAuditLogsQueryValidator : AbstractValidator<SearchAuditLogsQuery>
{
    public SearchAuditLogsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 1000);
        RuleFor(x => x)
            .Must(x => x.FromUtc is null || x.ToUtc is null || x.FromUtc <= x.ToUtc)
            .WithMessage("FromUtc phải trước hoặc bằng ToUtc.");
        RuleFor(x => x.Action).MaximumLength(200).When(x => x.Action is not null);
        RuleFor(x => x.EntityType).MaximumLength(200).When(x => x.EntityType is not null);
        RuleFor(x => x.EntityId).MaximumLength(200).When(x => x.EntityId is not null);
    }
}

public sealed class SearchAuditLogsQueryHandler
    : IRequestHandler<SearchAuditLogsQuery, Result<PagedList<AuditLogDto>>>
{
    private readonly IAuditLogReadRepository _auditLogs;

    public SearchAuditLogsQueryHandler(IAuditLogReadRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<Result<PagedList<AuditLogDto>>> Handle(SearchAuditLogsQuery request, CancellationToken ct)
    {
        var criteria = new AuditLogSearchCriteria(
            request.UserId,
            request.Action,
            request.EntityType,
            request.EntityId,
            request.FromUtc,
            request.ToUtc,
            request.Page,
            request.PageSize);

        var (items, total) = await _auditLogs.SearchAsync(criteria, ct);

        var dtos = items
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                UserId = x.UserId,
                Action = x.Action,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                IpAddress = x.IpAddress,
                OccurredAtUtc = x.OccurredAtUtc
            })
            .ToList();

        return Result<PagedList<AuditLogDto>>.Success(
            new PagedList<AuditLogDto>(dtos, total, request.Page, request.PageSize));
    }
}
