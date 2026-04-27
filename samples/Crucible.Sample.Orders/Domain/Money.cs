using Crucible.Domain.Aggregates;
using Crucible.Domain.Attributes;
using Crucible.Domain.Errors;
using Crucible.Domain.Results;

namespace Crucible.Sample.Orders.Domain;

[ValueObject]
public sealed partial record Money : ValueObject
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "";

    private Money() { }

    /// <summary>
    /// Validation rules for Money construction. Generator-emitted Create factory
    /// invokes this and surfaces failures as Result&lt;Money&gt;.Failure.
    /// </summary>
    private static partial Result __ValidateConstruction(decimal amount, string currency)
    {
        var errors = new System.Collections.Generic.List<IError>();
        if (amount < 0)
            errors.Add(new ValidationError("MONEY_NEGATIVE_AMOUNT", "Money amount must be non-negative", nameof(Amount)));
        if (string.IsNullOrWhiteSpace(currency))
            errors.Add(new ValidationError("MONEY_CURRENCY_REQUIRED", "Money currency is required", nameof(Currency)));
        return errors.Count > 0 ? Result.Failure(errors) : Result.Success();
    }

    /// <summary>Sanctioned trusted factory for currency-only zero values. Asserts the currency is valid.</summary>
    public static Money Zero(string currency)
    {
        var result = Create(0m, currency);
        return result.Match(
            money => money,
            errors => throw new ValueObjectException(
                $"Money.Zero called with invalid currency: {string.Join(",", errors.Select(e => e.ErrorCode))}"));
    }
}
