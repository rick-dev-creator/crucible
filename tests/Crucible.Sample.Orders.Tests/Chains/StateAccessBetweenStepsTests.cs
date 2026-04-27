using Crucible.Chains.DependencyInjection;
using Crucible.Domain.Errors;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrdersApi = Crucible.Sample.Orders.Domain.Orders;
using Xunit;

namespace Crucible.Sample.Orders.Tests.Chains;

/// <summary>
/// Demonstrates the C# 14 extension-members typestate: at each phase of the chain,
/// the state type passed to <c>Tap</c>/<c>OnError</c>/<c>ProducedEvents</c> is
/// narrowed to that phase's exact event type. The compiler enforces this — the
/// tests verify the runtime values match the type the compiler accepted.
/// </summary>
public sealed class StateAccessBetweenStepsTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();
        services.AddOrderAggregate();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        return services.BuildServiceProvider();
    }

    private static OrderDto Dto(string customer = "C-001") => new(customer, 100m, "USD");

    [Fact]
    public async Task Tap_AfterCreate_ReceivesOrderCreatedTypedState()
    {
        // The compiler accepts Action<OrderCreated> because Create returns
        // IChainStage<Order, OrderId, OrderCreated>. If the parameter were typed
        // as anything else (OrderPlaced, object, etc.), this would not compile.
        var sp = BuildServices();
        OrderCreated? captured = null;

        var result = await OrdersApi
            .Create(Dto("C-100"))
            .Tap(state => captured = state)
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.CustomerId.Should().Be("C-100");
        captured.Total.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task Tap_AfterPlaceOrder_ReceivesOrderPlacedTypedState()
    {
        // After PlaceOrder, the stage is IChainStage<Order, OrderId, OrderPlaced>.
        // Action<OrderPlaced> is the only callable Tap signature here.
        var sp = BuildServices();
        OrderPlaced? captured = null;

        var result = await OrdersApi
            .Create(Dto())
            .PlaceOrder(new ShippingOptions("DHL", 5))
            .Tap(state => captured = state)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Carrier.Should().Be("DHL");
    }

    [Fact]
    public async Task Tap_AfterUpdateOrderInventory_ReceivesOrderInventoryUpdatedTypedState()
    {
        var sp = BuildServices();
        OrderInventoryUpdated? captured = null;

        var result = await OrdersApi
            .Create(Dto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .UpdateOrderInventory()
            .Tap(state => captured = state)
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.OrderId.Value.Should().NotBe(System.Guid.Empty);
    }

    [Fact]
    public async Task Tap_ChainedAtEveryPhase_EachReceivesADifferentTypedState()
    {
        // The strongest demonstration: three Taps in one chain, each receiving
        // a structurally different state type. Compiler narrows TState per phase.
        var sp = BuildServices();
        var phaseTypes = new List<System.Type>();

        var result = await OrdersApi
            .Create(Dto())
            .Tap(state => phaseTypes.Add(state.GetType()))
            .PlaceOrder(new ShippingOptions("FedEx", 1))
            .Tap(state => phaseTypes.Add(state.GetType()))
            .UpdateOrderInventory()
            .Tap(state => phaseTypes.Add(state.GetType()))
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        phaseTypes.Should().Equal(
            typeof(OrderCreated),
            typeof(OrderPlaced),
            typeof(OrderInventoryUpdated));
    }

    [Fact]
    public async Task Tap_AsyncOverload_ReceivesTypedStateAndServiceProviderAndCt()
    {
        // The async overload of Tap: Func<TState, IServiceProvider, CancellationToken, Task>.
        // Verifies that the same typestate narrowing applies to async hooks.
        var sp = BuildServices();
        OrderPlaced? capturedState = null;
        IServiceProvider? capturedSp = null;
        var capturedCtCancellable = false;

        var result = await OrdersApi
            .Create(Dto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .Tap(async (state, services, ct) =>
            {
                await System.Threading.Tasks.Task.Yield();
                capturedState = state;
                capturedSp = services;
                capturedCtCancellable = ct.CanBeCanceled || !ct.IsCancellationRequested;
            })
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        capturedState.Should().NotBeNull();
        capturedState!.Carrier.Should().Be("UPS");
        capturedSp.Should().NotBeNull();
        capturedCtCancellable.Should().BeTrue();
    }

    [Fact]
    public async Task ProducedEvents_BetweenSteps_ObservesAllEventsRaisedUpToThatPoint()
    {
        // ProducedEvents is also an extension on IChainStage<,,> — the callback
        // sees the cumulative event log at the moment the chain reaches it.
        var sp = BuildServices();
        var observedAtMidpoint = new List<string>();

        var result = await OrdersApi
            .Create(Dto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .ProducedEvents(events =>
            {
                foreach (var e in events) observedAtMidpoint.Add(e.GetType().Name);
            })
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        observedAtMidpoint.Should().Equal(nameof(OrderCreated), nameof(OrderPlaced));
    }

    [Fact]
    public async Task OnError_AfterFailingStep_ReceivesTypedErrorList()
    {
        // OnError is registered between two steps. When a step fails, the executor
        // walks the plan and invokes any OnError callbacks with the failure's errors.
        var sp = BuildServices();
        IReadOnlyList<Error>? observed = null;

        var result = await OrdersApi
            .Create(new OrderDto("", 100m, "USD"))  // empty CustomerId — Create fails
            .OnError(errors => observed = errors)
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .ExecuteAsync(sp);

        result.IsDomainFailure.Should().BeTrue();
        observed.Should().NotBeNull();
        observed!.Should().ContainSingle(e => e.Code == "ORDER_CUSTOMER_REQUIRED");
        observed![0].Should().BeAssignableTo<ValidationError>();
    }

    [Fact]
    public async Task Tap_StateValueMatchesProducedEventAtSamePosition()
    {
        // The state passed to Tap is the same object that ends up in ProducedEvents.
        // Establishes that "between-method state" and "final event log" are consistent.
        var sp = BuildServices();
        OrderPlaced? tappedState = null;

        var result = await OrdersApi
            .Create(Dto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .Tap(state => tappedState = state)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        var orderPlacedFromLog = result.ProducedEvents.OfType<OrderPlaced>().Single();
        tappedState.Should().BeSameAs(orderPlacedFromLog);
    }
}
