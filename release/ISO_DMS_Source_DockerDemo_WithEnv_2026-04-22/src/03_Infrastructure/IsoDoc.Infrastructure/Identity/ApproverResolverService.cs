using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Enums;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Identity;

public sealed class ApproverResolverService : IApproverResolverService
{
    private readonly ApproverResolverOptions _options;

    public ApproverResolverService(IOptions<ApproverResolverOptions> options)
    {
        _options = options.Value;
    }

    public Task<(Guid Step1ApproverId, Guid Step2ApproverId)> ResolveAsync(
        IsoStandard standard,
        CancellationToken ct = default)
    {
        var step1 = standard switch
        {
            IsoStandard.ISO9001 => _options.QaOfficerId,
            IsoStandard.ISO45001 => _options.SafetyOfficerId,
            IsoStandard.ISO27001 => _options.IsmsOfficerId,
            _ => _options.QaOfficerId
        };

        return Task.FromResult((step1, _options.IsoManagerId));
    }
}

public sealed class ApproverResolverOptions
{
    public const string Section = "Approvers";

    public Guid QaOfficerId { get; set; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public Guid SafetyOfficerId { get; set; } = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public Guid IsmsOfficerId { get; set; } = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public Guid IsoManagerId { get; set; } = Guid.Parse("66666666-6666-6666-6666-666666666666");
}
