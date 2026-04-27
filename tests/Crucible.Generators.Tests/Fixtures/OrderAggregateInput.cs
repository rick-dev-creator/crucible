namespace Crucible.Generators.Tests.Fixtures;

internal static class OrderAggregateInput
{
    public const string Source = @"
using Crucible.Domain.Aggregates;
using Crucible.Domain.Attributes;
using Crucible.Domain.Events;
using Crucible.Domain.Identifiers;
using Crucible.Domain.Results;

namespace Sample;

public readonly record struct OrderId(System.Guid Value) : IAggregateId<OrderId>
{
    public static OrderId New() => new(System.Guid.NewGuid());
    public static OrderId From(System.Guid v) => new(v);
}

public sealed record OrderDto(string CustomerId);
public sealed record OrderCreated(OrderId Id) : DomainEvent;
public sealed record OrderPlaced(OrderId Id) : DomainEvent;

[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    [Step(Order = 1, Entry = true)]
    public Result<OrderCreated> Create(OrderDto dto)
    {
        Id = OrderId.New();
        Raise(new OrderCreated(Id));
        return new OrderCreated(Id);
    }

    [Step(Order = 2)]
    public Result<OrderPlaced> PlaceOrder()
    {
        Raise(new OrderPlaced(Id));
        return new OrderPlaced(Id);
    }
}
";
}
