using Crucible.Domain.Aggregates;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;

namespace Crucible.Chains.Steps;

public sealed class StepContext<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : IAggregateId<TId>
{
    public StepContext(IServiceProvider services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public TAggregate? Aggregate { get; set; }
    public IServiceProvider Services { get; }
    public List<IDomainEvent> AccumulatedEvents { get; } = new();
    public object? LastStepResult { get; set; }
}
