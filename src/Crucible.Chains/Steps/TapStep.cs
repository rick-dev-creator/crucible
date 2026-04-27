using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Steps;

internal sealed class TapStep<TAggregate, TId, TState> : IStep<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    private readonly Action<TState>? _sync;
    private readonly Func<TState, IServiceProvider, CancellationToken, Task>? _async;

    public TapStep(Action<TState> sync) => _sync = sync;
    public TapStep(Func<TState, IServiceProvider, CancellationToken, Task> @async) => _async = @async;

    public StepKind Kind => StepKind.Tap;
    public string Name => $"Tap<{typeof(TState).Name}>";

    public async Task<StepOutcome> InvokeAsync(StepContext<TAggregate, TId> context, CancellationToken ct)
    {
        if (context.LastStepResult is not TState state)
        {
            return StepOutcome.Success();
        }

        if (_sync is not null)
        {
            _sync(state);
        }
        else if (_async is not null)
        {
            await _async(state, context.Services, ct).ConfigureAwait(false);
        }

        return StepOutcome.Success();
    }
}
