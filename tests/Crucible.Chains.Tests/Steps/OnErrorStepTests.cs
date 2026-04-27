using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Errors;
using Crucible.Domain.Identifiers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crucible.Chains.Tests.Steps;

public sealed class OnErrorStepTests
{
    private readonly record struct TId(Guid Value) : IAggregateId<TId>
    { public static TId New() => new(Guid.NewGuid()); public static TId From(Guid g) => new(g); }
    private sealed class A : AggregateRoot<TId> { }

    [Fact]
    public async Task OnSuccessfulPath_DoesNothing()
    {
        var called = false;
        var step = new OnErrorStep<A, TId>(_ => called = true);
        var ctx = new StepContext<A, TId>(new ServiceCollection().BuildServiceProvider());
        var outcome = await step.InvokeAsync(ctx, CancellationToken.None);
        outcome.IsSuccess.Should().BeTrue();
        called.Should().BeFalse();
    }
}
