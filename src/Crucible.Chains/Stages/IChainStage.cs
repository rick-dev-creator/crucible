using Crucible.Chains.Results;
using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Errors;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Stages;

public interface IChainStage<TAggregate, TId, TState>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    ChainPlan<TAggregate, TId> Plan { get; }

    IChainStage<TAggregate, TId, TState> Tap(Action<TState> action);
    IChainStage<TAggregate, TId, TState> Tap(Func<TState, IServiceProvider, CancellationToken, Task> action);
    IChainStage<TAggregate, TId, TState> OnError(Action<IReadOnlyList<Error>> action);
    IChainStage<TAggregate, TId, TState> OnError(Func<IReadOnlyList<Error>, IServiceProvider, CancellationToken, Task> action);
    IChainStage<TAggregate, TId, TState> ProducedEvents(Action<IReadOnlyList<IDomainEvent>> callback, bool drain = true);
    IChainStage<TAggregate, TId, TState> DispatchEvents();

    Task<ChainResult<TState>> ExecuteAsync(IServiceProvider services, CancellationToken ct = default);
}

internal sealed class ChainStageImpl<TAggregate, TId, TState> : IChainStage<TAggregate, TId, TState>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    public ChainStageImpl(ChainPlan<TAggregate, TId> plan) => Plan = plan;
    public ChainPlan<TAggregate, TId> Plan { get; }

    public IChainStage<TAggregate, TId, TState> Tap(Action<TState> action)
        => ChainBuilder.AppendStep<TAggregate, TId, TState, TState>(this, new TapStep<TAggregate, TId, TState>(action));

    public IChainStage<TAggregate, TId, TState> Tap(Func<TState, IServiceProvider, CancellationToken, Task> action)
        => ChainBuilder.AppendStep<TAggregate, TId, TState, TState>(this, new TapStep<TAggregate, TId, TState>(action));

    public IChainStage<TAggregate, TId, TState> OnError(Action<IReadOnlyList<Error>> action)
        => ChainBuilder.AppendStep<TAggregate, TId, TState, TState>(this, new OnErrorStep<TAggregate, TId>(action));

    public IChainStage<TAggregate, TId, TState> OnError(Func<IReadOnlyList<Error>, IServiceProvider, CancellationToken, Task> action)
        => ChainBuilder.AppendStep<TAggregate, TId, TState, TState>(this, new OnErrorStep<TAggregate, TId>(action));

    public IChainStage<TAggregate, TId, TState> ProducedEvents(Action<IReadOnlyList<IDomainEvent>> callback, bool drain = true)
        => ChainBuilder.AppendStep<TAggregate, TId, TState, TState>(this, new ProducedEventsStep<TAggregate, TId>(callback, drain));

    public IChainStage<TAggregate, TId, TState> DispatchEvents()
        => ChainBuilder.AppendStep<TAggregate, TId, TState, TState>(this, new DispatchEventsStep<TAggregate, TId>());

    public Task<ChainResult<TState>> ExecuteAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var executor = (Execution.IChainExecutor?)services.GetService(typeof(Execution.IChainExecutor))
            ?? throw new InvalidOperationException("IChainExecutor is not registered. Call services.AddCrucible().");
        return executor.ExecuteAsync<TAggregate, TId, TState>(Plan, services, ct);
    }
}
