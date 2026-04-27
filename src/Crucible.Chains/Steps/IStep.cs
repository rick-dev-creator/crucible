using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Steps;

public interface IStep<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    StepKind Kind { get; }
    string Name { get; }
    Task<StepOutcome> InvokeAsync(StepContext<TAggregate, TId> context, CancellationToken ct);
}
