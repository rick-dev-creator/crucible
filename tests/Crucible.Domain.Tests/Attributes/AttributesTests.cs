using Crucible.Domain.Attributes;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Attributes;

public sealed class AttributesTests
{
    private sealed class DummyPre { }
    private sealed class DummyPost { }

    [Aggregate]
    private sealed class Tagged { }

    [Aggregate(EntryName = "Custom")]
    private sealed class TaggedWithEntry { }

    private sealed class Methods
    {
        [Step(Order = 1, Entry = true)]
        [Pre<DummyPre>]
        [Post<DummyPost>]
        public void Foo() { }
    }

    [Fact]
    public void AggregateAttribute_IsClassTargeted()
    {
        var attr = typeof(Tagged).GetCustomAttributes(typeof(AggregateAttribute), false);
        attr.Should().ContainSingle();
    }

    [Fact]
    public void AggregateAttribute_EntryName_RoundTrips()
    {
        var attr = (AggregateAttribute)typeof(TaggedWithEntry).GetCustomAttributes(typeof(AggregateAttribute), false)[0];
        attr.EntryName.Should().Be("Custom");
    }

    [Fact]
    public void StepAttribute_RoundTripsOrderAndEntry()
    {
        var method = typeof(Methods).GetMethod(nameof(Methods.Foo))!;
        var step = (StepAttribute)method.GetCustomAttributes(typeof(StepAttribute), false)[0];
        step.Order.Should().Be(1);
        step.Entry.Should().BeTrue();
    }

    [Fact]
    public void PreAndPostAttributes_AllowGenericArgument()
    {
        var method = typeof(Methods).GetMethod(nameof(Methods.Foo))!;
        method.GetCustomAttributes(typeof(PreAttribute<DummyPre>), false).Should().ContainSingle();
        method.GetCustomAttributes(typeof(PostAttribute<DummyPost>), false).Should().ContainSingle();
    }

    [Entity]
    private sealed class TaggedEntity { }

    [Fact]
    public void EntityAttribute_IsClassTargeted()
    {
        var attr = typeof(TaggedEntity).GetCustomAttributes(typeof(EntityAttribute), false);
        attr.Should().ContainSingle();
    }
}
