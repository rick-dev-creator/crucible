namespace Crucible.Sample.Orders.Domain;

public readonly record struct OrderItemId(System.Guid Value)
{
    public static OrderItemId New() => new(System.Guid.NewGuid());
    public static OrderItemId From(System.Guid value) => new(value);
}
