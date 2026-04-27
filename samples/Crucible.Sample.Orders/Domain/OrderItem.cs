using Crucible.Domain.Aggregates;
using Crucible.Domain.Attributes;
using Crucible.Domain.Errors;
using Crucible.Domain.Results;

namespace Crucible.Sample.Orders.Domain;

[Entity]
public partial class OrderItem : Entity<OrderItemId>
{
    public string ProductSku { get; private set; } = "";
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; } = Money.Zero("USD");

    private OrderItem() { }

    internal OrderItem(OrderItemId id, string sku, int qty, Money price)
    {
        Id = id;
        ProductSku = sku;
        Quantity = qty;
        UnitPrice = price;
    }

    public Result UpdateQuantity(int newQty)
    {
        if (newQty <= 0)
            return new ValidationError("ITEM_QTY_POSITIVE", "Quantity must be positive", nameof(newQty));
        Quantity = newQty;
        return Result.Success();
    }
}
