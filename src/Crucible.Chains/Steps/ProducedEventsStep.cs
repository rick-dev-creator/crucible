using Crucible.Domain.Aggregates;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Steps;

internal sealed class ProducedEventsStep<TAggregate, TId> : IStep<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    private readonly Action<IReadOnlyList<IDomainEvent>> _callback;
    private readonly bool _drain;

    public ProducedEventsStep(Action<IReadOnlyList<IDomainEvent>> callback, bool drain)
    {
        _callback = callback;
        _drain = drain;
    }

    public StepKind Kind => StepKind.ProducedEvents;
    public string Name => _drain ? "ProducedEvents(drain)" : "ProducedEvents(snapshot)";

    public Task<StepOutcome> InvokeAsync(StepContext<TAggregate, TId> context, CancellationToken ct)
    {
        var snapshot = context.AccumulatedEvents.ToArray();
        _callback(snapshot);
        if (_drain) context.AccumulatedEvents.Clear();
        return Task.FromResult(StepOutcome.Success());
    }
}
