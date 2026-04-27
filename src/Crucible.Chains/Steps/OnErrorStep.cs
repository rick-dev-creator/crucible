using Crucible.Domain.Aggregates;
using Crucible.Domain.Errors;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Steps;

internal sealed class OnErrorStep<TAggregate, TId> : IStep<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    private readonly Action<IReadOnlyList<Error>>? _sync;
    private readonly Func<IReadOnlyList<Error>, IServiceProvider, CancellationToken, Task>? _async;

    public OnErrorStep(Action<IReadOnlyList<Error>> sync) => _sync = sync;
    public OnErrorStep(Func<IReadOnlyList<Error>, IServiceProvider, CancellationToken, Task> @async) => _async = @async;

    public StepKind Kind => StepKind.OnError;
    public string Name => "OnError";

    public Action<IReadOnlyList<Error>>? SyncCallback => _sync;
    public Func<IReadOnlyList<Error>, IServiceProvider, CancellationToken, Task>? AsyncCallback => _async;

    public Task<StepOutcome> InvokeAsync(StepContext<TAggregate, TId> context, CancellationToken ct)
        => Task.FromResult(StepOutcome.Success());
}
