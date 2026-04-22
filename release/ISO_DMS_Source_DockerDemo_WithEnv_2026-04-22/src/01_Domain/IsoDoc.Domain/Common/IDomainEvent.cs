using System;
using MediatR;

namespace IsoDoc.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// Implements MediatR INotification so handlers can be registered and dispatched.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Convenience base record for domain events.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

