using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Processors;

public sealed class PostContext<TAggregate, TId, TOutput>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    public PostContext(TAggregate aggregate, TOutput output, IServiceProvider services)
    {
        Aggregate = aggregate;
        Output = output;
        Services = services;
    }

    public TAggregate Aggregate { get; }
    public TOutput Output { get; }
    public IServiceProvider Services { get; }
}
