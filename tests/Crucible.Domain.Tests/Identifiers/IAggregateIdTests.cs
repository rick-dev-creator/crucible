using Crucible.Domain.Identifiers;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Identifiers;

public sealed class IAggregateIdTests
{
    private readonly record struct TestId(Guid Value) : IAggregateId<TestId>
    {
        public static TestId New() => new(Guid.NewGuid());
        public static TestId From(Guid value) => new(value);
    }

    [Fact]
    public void New_ReturnsDistinctValuesAcrossCalls()
    {
        var a = TestId.New();
        var b = TestId.New();
        a.Value.Should().NotBe(Guid.Empty);
        b.Value.Should().NotBe(a.Value);
    }

    [Fact]
    public void From_PreservesProvidedGuid()
    {
        var g = Guid.NewGuid();
        TestId.From(g).Value.Should().Be(g);
    }
}
