using Crucible.Chains.Results;
using Crucible.Chains.Stages;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Execution;

public interface IChainExecutor
{
    Task<ChainResult<TState>> ExecuteAsync<TAggregate, TId, TState>(
        ChainPlan<TAggregate, TId> plan,
        IServiceProvider services,
        CancellationToken ct)
        where TAggregate : AggregateRoot<TId>
        where TId : IAggregateId<TId>;
}
