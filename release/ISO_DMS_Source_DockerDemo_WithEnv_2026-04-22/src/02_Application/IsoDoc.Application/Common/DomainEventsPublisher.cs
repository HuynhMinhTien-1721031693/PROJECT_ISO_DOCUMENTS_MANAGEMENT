using IsoDoc.Domain.Common;
using MediatR;

namespace IsoDoc.Application.Common;

public static class DomainEventsPublisher
{
    public static async Task PublishAndClearAsync(IMediator mediator, BaseEntity entity, CancellationToken ct)
    {
        var events = entity.DomainEvents.ToList();
        entity.ClearDomainEvents();
        foreach (var e in events)
            await mediator.Publish(e, ct);
    }

    public static async Task PublishAndClearAsync(IMediator mediator, IEnumerable<BaseEntity> entities, CancellationToken ct)
    {
        foreach (var entity in entities)
            await PublishAndClearAsync(mediator, entity, ct);
    }
}
