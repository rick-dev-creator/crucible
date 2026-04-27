using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;
using Crucible.Domain.Results;

namespace Crucible.Chains.Handlers;

public interface IStepHandler<TAggregate, TId, in TInput, in TOutput>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    Task<Result> InvokeAsync(TAggregate aggregate, TInput input, TOutput stepResult, CancellationToken ct);
}
