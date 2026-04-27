using Crucible.Sample.Orders.Domain;
using FluentAssertions;
using Xunit;

namespace Crucible.Sample.Orders.Tests.Domain;

public sealed class OrderItemEntityTests
{
    private sealed record TestSnapshot(OrderItemId Id, string ProductSku, int Quantity, Money UnitPrice) : IOrderItemSnapshot;

    [Fact]
    public void RehydrateFrom_PopulatesAllProperties()
    {
        var snap = new TestSnapshot(
            Id: OrderItemId.From(System.Guid.NewGuid()),
            ProductSku: "SKU-001",
            Quantity: 3,
            UnitPrice: new Money(25m, "USD"));

        var item = OrderItem.RehydrateFrom(snap);

        item.Id.Should().Be(snap.Id);
        item.ProductSku.Should().Be("SKU-001");
        item.Quantity.Should().Be(3);
        item.UnitPrice.Should().Be(new Money(25m, "USD"));
    }

    [Fact]
    public void Equality_IsBasedOnId()
    {
        var sharedId = OrderItemId.From(System.Guid.NewGuid());
        var a = new OrderItem(sharedId, "SKU-A", 1, new Money(10m, "USD"));
        var b = new OrderItem(sharedId, "SKU-B", 99, new Money(99m, "USD"));

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void UpdateQuantity_WithPositive_AllowsAndUpdates()
    {
        var item = new OrderItem(OrderItemId.New(), "SKU-1", 1, new Money(10m, "USD"));
        var result = item.UpdateQuantity(5);
        result.IsSuccess.Should().BeTrue();
        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateQuantity_WithZero_ReturnsValidationError()
    {
        var item = new OrderItem(OrderItemId.New(), "SKU-1", 1, new Money(10m, "USD"));
        var result = item.UpdateQuantity(0);
        result.IsFailure.Should().BeTrue();
        result.Errors[0].Code.Should().Be("ITEM_QTY_POSITIVE");
    }
}
