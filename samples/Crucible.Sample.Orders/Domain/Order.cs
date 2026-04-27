using Crucible.Domain.Aggregates;
using Crucible.Domain.Attributes;
using Crucible.Domain.Errors;
using Crucible.Domain.Results;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.Processors;

namespace Crucible.Sample.Orders.Domain;

[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private Order() { }

    public string CustomerId { get; private set; } = "";
    public Money Total { get; private set; } = Money.Zero("USD");
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public string? Carrier { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items;

    [Step(Order = 1, Entry = true)]
    [Pre<ValidateCustomerPreProcessor>]
    public Result<OrderCreated> Create(OrderDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
            return new ValidationError("ORDER_CUSTOMER_REQUIRED", "CustomerId is required", nameof(dto.CustomerId));
        if (dto.Amount <= 0)
            return new ValidationError("ORDER_AMOUNT_POSITIVE", "Amount must be positive", nameof(dto.Amount));

        var totalResult = Money.Create(dto.Amount, dto.Currency);
        if (totalResult.IsFailure) return Result<OrderCreated>.Failure(totalResult.Errors);

        Id = OrderId.New();
        CustomerId = dto.CustomerId;
        Total = totalResult.Value;
        Status = OrderStatus.Draft;

        var evt = new OrderCreated(Id, CustomerId, Total);
        Raise(evt);
        return evt;
    }

    [Step(Order = 2)]
    public Result<OrderPlaced> PlaceOrder(ShippingOptions shipping)
    {
        if (Status != OrderStatus.Draft)
            return new BusinessRuleError("ORDER_NOT_DRAFT", "Order must be in Draft status to place");

        Carrier = shipping.Carrier;
        Status = OrderStatus.Placed;

        var evt = new OrderPlaced(Id, shipping.Carrier);
        Raise(evt);
        return evt;
    }

    [Step(Order = 3)]
    public Result<OrderInventoryUpdated> UpdateOrderInventory()
    {
        if (Status != OrderStatus.Placed)
            return new BusinessRuleError("ORDER_NOT_PLACED", "Order must be Placed before inventory update");

        Status = OrderStatus.InventoryReserved;

        var evt = new OrderInventoryUpdated(Id);
        Raise(evt);
        return evt;
    }
}
