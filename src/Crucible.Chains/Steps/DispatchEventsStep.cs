using Crucible.Domain.Aggregates;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;
using Microsoft.Extensions.DependencyInjection;

namespace Crucible.Chains.Steps;

internal sealed class DispatchEventsStep<TAggregate, TId> : IStep<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    public StepKind Kind => StepKind.DispatchEvents;
    public string Name => "DispatchEvents";

    public async Task<StepOutcome> InvokeAsync(StepContext<TAggregate, TId> context, CancellationToken ct)
    {
        if (context.AccumulatedEvents.Count == 0) return StepOutcome.Success();

        var dispatcher = context.Services.GetRequiredService<IDomainEventDispatcher>();
        var snapshot = context.AccumulatedEvents.ToArray();
        await dispatcher.DispatchAsync(snapshot, ct).ConfigureAwait(false);
        context.AccumulatedEvents.Clear();
        return StepOutcome.Success();
    }
}
