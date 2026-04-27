using Crucible.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Aggregates;

public sealed class EntityTests
{
    private sealed class Person : Entity<Guid>
    {
        public Person(Guid id) => Id = id;
    }

    [Fact]
    public void Equals_WhenIdsMatch_IsTrue()
    {
        var id = Guid.NewGuid();
        new Person(id).Equals(new Person(id)).Should().BeTrue();
    }

    [Fact]
    public void Equals_WhenIdsDiffer_IsFalse()
    {
        new Person(Guid.NewGuid()).Equals(new Person(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_MatchesIdHashCode()
    {
        var id = Guid.NewGuid();
        new Person(id).GetHashCode().Should().Be(id.GetHashCode());
    }
}
