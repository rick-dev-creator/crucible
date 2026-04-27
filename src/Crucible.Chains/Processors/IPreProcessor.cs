using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;
using Crucible.Domain.Results;

namespace Crucible.Chains.Processors;

public interface IPreProcessor<TAggregate, TId, TInput>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    Task<Result> InvokeAsync(PreContext<TAggregate, TId, TInput> context, CancellationToken ct);
}
