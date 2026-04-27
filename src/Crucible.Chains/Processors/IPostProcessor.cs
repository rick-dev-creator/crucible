using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Processors;

public interface IPostProcessor<TAggregate, TId, TOutput>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    Task InvokeAsync(PostContext<TAggregate, TId, TOutput> context, CancellationToken ct);
}
