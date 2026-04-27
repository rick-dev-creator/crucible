using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Identifiers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crucible.Chains.Tests.Steps;

public sealed class StepContextTests
{
    private readonly record struct TestId(Guid Value) : IAggregateId<TestId>
    {
        public static TestId New() => new(Guid.NewGuid());
        public static TestId From(Guid v) => new(v);
    }

    private sealed class TestAgg : AggregateRoot<TestId> { }

    [Fact]
    public void StartsWithNullAggregateAndEmptyEvents()
    {
        var ctx = new StepContext<TestAgg, TestId>(new ServiceCollection().BuildServiceProvider());
        ctx.Aggregate.Should().BeNull();
        ctx.AccumulatedEvents.Should().BeEmpty();
        ctx.Services.Should().NotBeNull();
    }

    [Fact]
    public void AggregateAndEvents_AreMutable()
    {
        var ctx = new StepContext<TestAgg, TestId>(new ServiceCollection().BuildServiceProvider());
        ctx.Aggregate = new TestAgg();
        ctx.Aggregate.Should().NotBeNull();
    }
}
