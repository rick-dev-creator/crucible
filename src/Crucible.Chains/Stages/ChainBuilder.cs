using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Stages;

public static class ChainBuilder
{
    public static IChainStage<TAggregate, TId, TNextState> AppendStep<TAggregate, TId, TPrevState, TNextState>(
        IChainStage<TAggregate, TId, TPrevState> stage,
        IStep<TAggregate, TId> step)
        where TAggregate : AggregateRoot<TId>
        where TId : IAggregateId<TId>
    {
        ((ChainStageImpl<TAggregate, TId, TPrevState>)stage).Plan.Add(step);

        if (typeof(TPrevState) == typeof(TNextState))
        {
            return (IChainStage<TAggregate, TId, TNextState>)(object)stage;
        }

        return new ChainStageImpl<TAggregate, TId, TNextState>(stage.Plan);
    }

    public static IChainStage<TAggregate, TId, TInitialState> Begin<TAggregate, TId, TInitialState>(
        IStep<TAggregate, TId> entryStep)
        where TAggregate : AggregateRoot<TId>
        where TId : IAggregateId<TId>
    {
        var plan = new ChainPlan<TAggregate, TId>();
        plan.Add(entryStep);
        return new ChainStageImpl<TAggregate, TId, TInitialState>(plan);
    }
}
