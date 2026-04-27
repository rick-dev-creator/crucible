using Crucible.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Aggregates;

public sealed class ValueObjectTests
{
    private sealed record Money(decimal Amount, string Currency) : ValueObject;

    [Fact]
    public void RecordEquality_HoldsForEqualFields()
    {
        new Money(10, "USD").Should().Be(new Money(10, "USD"));
    }

    [Fact]
    public void RecordEquality_FailsForDifferentFields()
    {
        new Money(10, "USD").Should().NotBe(new Money(11, "USD"));
    }

    [Fact]
    public void ValueObjectException_PreservesMessage()
    {
        var ex = new ValueObjectException("oops");
        ex.Message.Should().Be("oops");
    }
}
