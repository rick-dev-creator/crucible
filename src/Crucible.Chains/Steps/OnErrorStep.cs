using Crucible.Domain.Aggregates;
using Crucible.Domain.Errors;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Steps;

internal sealed class OnErrorStep<TAggregate, TId> : IStep<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    private readonly Action<IReadOnlyList<IError>>? _sync;
    private readonly Func<IReadOnlyList<IError>, IServiceProvider, CancellationToken, Task>? _async;

    public OnErrorStep(Action<IReadOnlyList<IError>> sync) => _sync = sync;
    public OnErrorStep(Func<IReadOnlyList<IError>, IServiceProvider, CancellationToken, Task> @async) => _async = @async;

    public StepKind Kind => StepKind.OnError;
    public string Name => "OnError";

    public Action<IReadOnlyList<IError>>? SyncCallback => _sync;
    public Func<IReadOnlyList<IError>, IServiceProvider, CancellationToken, Task>? AsyncCallback => _async;

    public Task<StepOutcome> InvokeAsync(StepContext<TAggregate, TId> context, CancellationToken ct)
        => Task.FromResult(StepOutcome.Success());
}
