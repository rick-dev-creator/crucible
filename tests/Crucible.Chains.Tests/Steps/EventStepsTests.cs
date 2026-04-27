using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crucible.Chains.Tests.Steps;

public sealed class EventStepsTests
{
    private readonly record struct TId(Guid Value) : IAggregateId<TId>
    { public static TId New() => new(Guid.NewGuid()); public static TId From(Guid g) => new(g); }
    private sealed class A : AggregateRoot<TId> { }
    private sealed record E(string Tag) : DomainEvent;

    [Fact]
    public async Task ProducedEventsStep_Drain_VacatesAndPassesSnapshot()
    {
        var ctx = new StepContext<A, TId>(new ServiceCollection().BuildServiceProvider());
        ctx.AccumulatedEvents.Add(new E("a"));
        ctx.AccumulatedEvents.Add(new E("b"));
        var observed = Array.Empty<IDomainEvent>();
        var step = new ProducedEventsStep<A, TId>(events => observed = events.ToArray(), drain: true);
        var outcome = await step.InvokeAsync(ctx, CancellationToken.None);
        outcome.IsSuccess.Should().BeTrue();
        observed.Should().HaveCount(2);
        ctx.AccumulatedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task ProducedEventsStep_Snapshot_LeavesQueueIntact()
    {
        var ctx = new StepContext<A, TId>(new ServiceCollection().BuildServiceProvider());
        ctx.AccumulatedEvents.Add(new E("a"));
        var step = new ProducedEventsStep<A, TId>(_ => { }, drain: false);
        await step.InvokeAsync(ctx, CancellationToken.None);
        ctx.AccumulatedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task DispatchEventsStep_DelegatesToDispatcherAndDrains()
    {
        var dispatched = new List<IDomainEvent>();
        var fakeDispatcher = new FakeDispatcher(dispatched);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher>(fakeDispatcher);
        var ctx = new StepContext<A, TId>(services.BuildServiceProvider());
        ctx.AccumulatedEvents.Add(new E("x"));

        var step = new DispatchEventsStep<A, TId>();
        var outcome = await step.InvokeAsync(ctx, CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        dispatched.Should().HaveCount(1);
        ctx.AccumulatedEvents.Should().BeEmpty();
    }

    private sealed class FakeDispatcher : IDomainEventDispatcher
    {
        private readonly List<IDomainEvent> _captured;
        public FakeDispatcher(List<IDomainEvent> captured) => _captured = captured;
        public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
        {
            _captured.AddRange(events);
            return Task.CompletedTask;
        }
    }
}
