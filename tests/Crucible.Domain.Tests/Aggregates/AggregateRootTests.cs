using Crucible.Domain.Aggregates;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Aggregates;

public sealed class AggregateRootTests
{
    private readonly record struct SampleId(Guid Value) : IAggregateId<SampleId>
    {
        public static SampleId New() => new(Guid.NewGuid());
        public static SampleId From(Guid value) => new(value);
    }

    private sealed record SampleEvent(string What) : DomainEvent;

    private sealed class Sample : AggregateRoot<SampleId>
    {
        public Sample() => Id = SampleId.New();
        public void Do(string what) => Raise(new SampleEvent(what));
        public void IncrementVersionForTest() => Version++;
        public void ClearForTest() => ClearPendingEvents();
    }

    [Fact]
    public void Raise_AddsEventToPendingList()
    {
        var s = new Sample();
        s.Do("hello");
        s.PendingEvents.Should().ContainSingle()
            .Which.Should().BeOfType<SampleEvent>()
            .Which.What.Should().Be("hello");
    }

    [Fact]
    public void Raise_PreservesOrder()
    {
        var s = new Sample();
        s.Do("a");
        s.Do("b");
        s.Do("c");
        s.PendingEvents.Select(e => ((SampleEvent)e).What).Should().Equal("a", "b", "c");
    }

    [Fact]
    public void ClearPendingEvents_EmptiesTheList()
    {
        var s = new Sample();
        s.Do("x");
        s.ClearForTest();
        s.PendingEvents.Should().BeEmpty();
    }

    [Fact]
    public void Version_StartsAtZero()
    {
        new Sample().Version.Should().Be(0);
    }
}
