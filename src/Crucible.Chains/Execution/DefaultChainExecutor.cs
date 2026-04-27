using Crucible.Chains.Behaviors;
using Crucible.Chains.Results;
using Crucible.Chains.Stages;
using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Execution;

public sealed class DefaultChainExecutor : IChainExecutor
{
    private readonly StepBehaviorPipeline _pipeline;

    public DefaultChainExecutor(StepBehaviorPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task<ChainResult<TState>> ExecuteAsync<TAggregate, TId, TState>(
        ChainPlan<TAggregate, TId> plan,
        IServiceProvider services,
        CancellationToken ct)
        where TAggregate : AggregateRoot<TId>
        where TId : IAggregateId<TId>
    {
        var ctx = new StepContext<TAggregate, TId>(services);

        try
        {
            foreach (var step in plan.Steps)
            {
                ct.ThrowIfCancellationRequested();

                // OnError steps fire only when the chain has already failed.
                // In a successful run they're skipped.
                if (step.Kind == StepKind.OnError) continue;

                var descriptor = new StepDescriptor(plan.AggregateName, step.Name, step.Kind, typeof(TAggregate), null);
                var outcome = await _pipeline.RunAsync(
                    descriptor,
                    () => step.InvokeAsync(ctx, ct),
                    services,
                    ct).ConfigureAwait(false);

                if (outcome.IsFailure)
                {
                    await InvokeOnErrorStepsAsync(plan, outcome.Errors, services, ct).ConfigureAwait(false);
                    var snapshot = ctx.AccumulatedEvents.ToArray();
                    return ChainResult<TState>.DomainFailure(outcome.Errors, snapshot);
                }

                if (outcome.Result is not null)
                {
                    ctx.LastStepResult = outcome.Result;
                }

                // Drain pending events + bump version only on aggregate-method steps.
                // Other step kinds (Tap/ProducedEvents/DispatchEvents) don't mutate the aggregate,
                // so Version stays put.
                if (step.Kind == StepKind.AggregateMethod
                    && ctx.Aggregate is not null
                    && ctx.Aggregate.PendingEvents.Count > 0)
                {
                    ctx.AccumulatedEvents.AddRange(ctx.Aggregate.PendingEvents);
                    ctx.Aggregate.ClearPendingEvents();
                    ctx.Aggregate.Version++;
                }
            }

            var finalEvents = ctx.AccumulatedEvents.ToArray();
            var typedResult = ctx.LastStepResult is TState s ? s : default!;
            return ChainResult<TState>.Success(typedResult, finalEvents);
        }
        catch (Exception ex)
        {
            return ChainResult<TState>.Exceptional(ex, ctx.AccumulatedEvents.ToArray());
        }
    }

    private static async Task InvokeOnErrorStepsAsync<TAggregate, TId>(
        ChainPlan<TAggregate, TId> plan,
        IReadOnlyList<Crucible.Domain.Errors.Error> errors,
        IServiceProvider services,
        CancellationToken ct)
        where TAggregate : AggregateRoot<TId>
        where TId : IAggregateId<TId>
    {
        foreach (var step in plan.Steps)
        {
            if (step is OnErrorStep<TAggregate, TId> onError)
            {
                if (onError.SyncCallback is not null) onError.SyncCallback(errors);
                if (onError.AsyncCallback is not null) await onError.AsyncCallback(errors, services, ct).ConfigureAwait(false);
            }
        }
    }
}
