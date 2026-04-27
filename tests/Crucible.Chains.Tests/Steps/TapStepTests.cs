using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crucible.Chains.Tests.Steps;

public sealed class TapStepTests
{
    private readonly record struct TId(Guid Value) : IAggregateId<TId>
    { public static TId New() => new(Guid.NewGuid()); public static TId From(Guid g) => new(g); }
    private sealed class A : AggregateRoot<TId> { }

    [Fact]
    public async Task Sync_Action_IsInvokedWithLastResult()
    {
        var seen = "";
        var step = new TapStep<A, TId, string>(s => seen = s);
        var ctx = new StepContext<A, TId>(new ServiceCollection().BuildServiceProvider()) { LastStepResult = "hello" };
        var outcome = await step.InvokeAsync(ctx, CancellationToken.None);
        outcome.IsSuccess.Should().BeTrue();
        seen.Should().Be("hello");
    }

    [Fact]
    public async Task Async_Action_IsAwaited()
    {
        var seen = false;
        var step = new TapStep<A, TId, string>(async (s, sp, ct) => { await Task.Yield(); seen = true; });
        var ctx = new StepContext<A, TId>(new ServiceCollection().BuildServiceProvider()) { LastStepResult = "x" };
        await step.InvokeAsync(ctx, CancellationToken.None);
        seen.Should().BeTrue();
    }
}
