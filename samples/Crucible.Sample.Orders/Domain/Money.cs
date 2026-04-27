using Crucible.Domain.Aggregates;

namespace Crucible.Sample.Orders.Domain;

public sealed record Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ValueObjectException("Money.Amount cannot be negative");
        if (string.IsNullOrWhiteSpace(currency)) throw new ValueObjectException("Money.Currency required");
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency) => new(0, currency);
}
