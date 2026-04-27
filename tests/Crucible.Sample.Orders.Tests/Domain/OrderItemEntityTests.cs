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
            UnitPrice: Money.Create(25m, "USD").Value);

        var item = OrderItem.RehydrateFrom(snap);

        item.Id.Should().Be(snap.Id);
        item.ProductSku.Should().Be("SKU-001");
        item.Quantity.Should().Be(3);
        item.UnitPrice.Should().Be(Money.Create(25m, "USD").Value);
    }

    [Fact]
    public void Equality_IsBasedOnId()
    {
        var sharedId = OrderItemId.From(System.Guid.NewGuid());
        var a = new OrderItem(sharedId, "SKU-A", 1, Money.Create(10m, "USD").Value);
        var b = new OrderItem(sharedId, "SKU-B", 99, Money.Create(99m, "USD").Value);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void UpdateQuantity_WithPositive_AllowsAndUpdates()
    {
        var item = new OrderItem(OrderItemId.New(), "SKU-1", 1, Money.Create(10m, "USD").Value);
        var result = item.UpdateQuantity(5);
        result.IsSuccess.Should().BeTrue();
        item.Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateQuantity_WithZero_ReturnsValidationError()
    {
        var item = new OrderItem(OrderItemId.New(), "SKU-1", 1, Money.Create(10m, "USD").Value);
        var result = item.UpdateQuantity(0);
        result.IsFailure.Should().BeTrue();
        result.Errors[0].ErrorCode.Should().Be("ITEM_QTY_POSITIVE");
    }
}
