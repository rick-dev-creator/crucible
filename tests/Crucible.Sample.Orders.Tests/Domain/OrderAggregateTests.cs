using Crucible.Domain.Errors;
using Crucible.Domain.Events;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Crucible.Sample.Orders.Tests.Domain;

public sealed class OrderAggregateTests
{
    private static OrderDto ValidDto(string customerId = "C-001") => new(customerId, 100m, "USD");

    [Fact]
    public void Create_WithValidDto_SetsIdAndCustomerAndDraftStatus()
    {
        var order = Order.__CreateForChain();

        var result = order.Create(ValidDto("C-100"));

        result.IsSuccess.Should().BeTrue();
        order.Id.Value.Should().NotBe(System.Guid.Empty);
        order.CustomerId.Should().Be("C-100");
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ReturnsValidationError()
    {
        var order = Order.__CreateForChain();

        var result = order.Create(ValidDto(""));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "ORDER_CUSTOMER_REQUIRED");
        result.Errors[0].Should().BeOfType<ValidationError>();
    }

    [Fact]
    public void Create_WithNonPositiveAmount_ReturnsValidationError()
    {
        var order = Order.__CreateForChain();

        var result = order.Create(new OrderDto("C-1", 0m, "USD"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "ORDER_AMOUNT_POSITIVE");
    }

    [Fact]
    public void Create_RaisesOrderCreatedEventWithMatchingTotal()
    {
        var order = Order.__CreateForChain();

        order.Create(new OrderDto("C-1", 250m, "EUR"));

        order.PendingEvents.Should().HaveCount(1);
        var evt = order.PendingEvents[0].Should().BeOfType<OrderCreated>().Subject;
        evt.OrderId.Should().Be(order.Id);
        evt.CustomerId.Should().Be("C-1");
        evt.Total.Amount.Should().Be(250m);
        evt.Total.Currency.Should().Be("EUR");
    }

    [Fact]
    public void PlaceOrder_OnDraftOrder_TransitionsToPlacedAndRecordsCarrier()
    {
        var order = Order.__CreateForChain();
        order.Create(ValidDto());

        var result = order.PlaceOrder(new ShippingOptions("UPS", 2));

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Placed);
        order.Carrier.Should().Be("UPS");
    }

    [Fact]
    public void PlaceOrder_OnAlreadyPlacedOrder_ReturnsBusinessRuleError()
    {
        var order = Order.__CreateForChain();
        order.Create(ValidDto());
        order.PlaceOrder(new ShippingOptions("UPS", 2));

        var result = order.PlaceOrder(new ShippingOptions("FedEx", 1));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "ORDER_NOT_DRAFT");
        result.Errors[0].Should().BeOfType<BusinessRuleError>();
    }

    [Fact]
    public void PlaceOrder_RaisesOrderPlacedEventInAdditionToOrderCreated()
    {
        var order = Order.__CreateForChain();
        order.Create(ValidDto());

        order.PlaceOrder(new ShippingOptions("DHL", 3));

        order.PendingEvents.Select(e => e.GetType().Name).Should().Equal(
            nameof(OrderCreated), nameof(OrderPlaced));
        ((OrderPlaced)order.PendingEvents[1]).Carrier.Should().Be("DHL");
    }

    [Fact]
    public void UpdateOrderInventory_OnPlacedOrder_TransitionsToInventoryReserved()
    {
        var order = Order.__CreateForChain();
        order.Create(ValidDto());
        order.PlaceOrder(new ShippingOptions("UPS", 2));

        var result = order.UpdateOrderInventory();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.InventoryReserved);
    }

    [Fact]
    public void UpdateOrderInventory_OnDraftOrder_ReturnsBusinessRuleError()
    {
        var order = Order.__CreateForChain();
        order.Create(ValidDto());

        var result = order.UpdateOrderInventory();

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Code == "ORDER_NOT_PLACED");
    }

    [Fact]
    public void PendingEvents_PreservesChronologicalOrderAcrossMultipleMethods()
    {
        var order = Order.__CreateForChain();

        order.Create(ValidDto());
        order.PlaceOrder(new ShippingOptions("UPS", 2));
        order.UpdateOrderInventory();

        order.PendingEvents.Select(e => e.GetType()).Should().Equal(
            typeof(OrderCreated), typeof(OrderPlaced), typeof(OrderInventoryUpdated));
    }
}
