using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Stages;

public sealed class ChainPlan<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    private readonly List<IStep<TAggregate, TId>> _steps = new();
    public IReadOnlyList<IStep<TAggregate, TId>> Steps => _steps;
    public string AggregateName { get; init; } = typeof(TAggregate).Name;

    internal void Add(IStep<TAggregate, TId> step) => _steps.Add(step);
}
