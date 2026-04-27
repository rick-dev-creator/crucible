using Crucible.Domain.Errors;
using Crucible.Sample.Orders.Domain;
using FluentAssertions;
using Xunit;

namespace Crucible.Sample.Orders.Tests.Domain;

public sealed class MoneyValueObjectTests
{
    [Fact]
    public void Create_WithValidAmountAndCurrency_ReturnsSuccess()
    {
        var result = Money.Create(100m, "USD");
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(100m);
        result.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithNegativeAmount_ReturnsValidationError()
    {
        var result = Money.Create(-1m, "USD");
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.ErrorCode == "MONEY_NEGATIVE_AMOUNT");
    }

    [Fact]
    public void Create_WithEmptyCurrency_ReturnsValidationError()
    {
        var result = Money.Create(10m, "");
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.ErrorCode == "MONEY_CURRENCY_REQUIRED");
    }

    [Fact]
    public void Create_WithMultipleInvalidFields_ReturnsAllErrors()
    {
        var result = Money.Create(-5m, "");
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.ErrorCode == "MONEY_NEGATIVE_AMOUNT");
        result.Errors.Should().Contain(e => e.ErrorCode == "MONEY_CURRENCY_REQUIRED");
    }

    [Fact]
    public void Equality_IsStructural()
    {
        var a = Money.Create(50m, "EUR").Value;
        var b = Money.Create(50m, "EUR").Value;
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Zero_WithValidCurrency_ReturnsZeroAmountMoney()
    {
        var zero = Money.Zero("USD");
        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("USD");
    }
}
