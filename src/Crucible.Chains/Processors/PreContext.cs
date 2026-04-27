using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Processors;

public sealed class PreContext<TAggregate, TId, TInput>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    public PreContext(TAggregate aggregate, TInput input, IServiceProvider services)
    {
        Aggregate = aggregate;
        Input = input;
        Services = services;
    }

    public TAggregate Aggregate { get; }
    public TInput Input { get; }
    public IServiceProvider Services { get; }
}
