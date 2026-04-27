using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;

namespace Crucible.Domain.Aggregates;

public abstract class AggregateRoot<TId> where TId : IAggregateId<TId>
{
    public TId Id { get; protected set; } = default!;
    public long Version { get; internal set; }

    private readonly List<IDomainEvent> _pendingEvents = new();
    public IReadOnlyList<IDomainEvent> PendingEvents => _pendingEvents;

    protected void Raise(IDomainEvent @event) => _pendingEvents.Add(@event);

    internal void ClearPendingEvents() => _pendingEvents.Clear();
}
