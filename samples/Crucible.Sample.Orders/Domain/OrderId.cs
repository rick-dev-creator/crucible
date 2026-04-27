using Crucible.Domain.Identifiers;

namespace Crucible.Sample.Orders.Domain;

public readonly record struct OrderId(Guid Value) : IAggregateId<OrderId>
{
    public static OrderId New() => new(Guid.NewGuid());
    public static OrderId From(Guid value) => new(value);
}
