using Crucible.Chains.DependencyInjection;
using Crucible.Chains.Handlers;
using Crucible.Domain.Errors;
using Crucible.Domain.Events;
using Crucible.Domain.Results;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.EventHandlers;
using Crucible.Sample.Orders.Infrastructure;
using FluentAssertions;
using OrdersApi = Crucible.Sample.Orders.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Crucible.Sample.Orders.Tests.Chains;

public sealed class OrderChainTests
{
    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();
        services.AddOrderAggregate();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddCrucibleEventHandler<OrderPlaced, NotifyWarehouseHandler>();
        return services.BuildServiceProvider();
    }

    private static OrderDto ValidDto(string customer = "C-001") => new(customer, 100m, "USD");

    [Fact]
    public async Task FullChain_ProducesAllThreeDomainEventsInChronologicalOrder()
    {
        var sp = BuildServices();

        var result = await OrdersApi
            .Create(ValidDto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        result.ProducedEvents.Select(e => e.GetType()).Should().Equal(
            typeof(OrderCreated), typeof(OrderPlaced), typeof(OrderInventoryUpdated));
    }

    [Fact]
    public async Task Chain_PlaceOrderHandlerFails_StillSurfacesEventsFromCreate()
    {
        // The chain type system enforces step order at compile time — you cannot skip PlaceOrder.
        // We simulate a domain failure at PlaceOrder's handler level (after Create succeeds)
        // by registering a failing handler, proving events from Create are preserved.
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();
        // Register the failing handler BEFORE AddOrderAggregate so TryAddScoped keeps ours.
        services.AddScoped<IStepHandler<Order, OrderId, ShippingOptions, OrderPlaced>, FailingPlaceOrderHandler>();
        services.AddOrderAggregate();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        var sp = services.BuildServiceProvider();

        var failingResult = await OrdersApi
            .Create(ValidDto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .ExecuteAsync(sp);

        failingResult.IsDomainFailure.Should().BeTrue();
        failingResult.Match(
            success: _ => "should not be success",
            failure: errs => errs[0].Code).Should().Be("PLACEMENT_REJECTED");

        // OrderCreated was raised by the successful Create step before PlaceOrder's handler failed.
        failingResult.ProducedEvents.Should().ContainSingle(e => e is OrderCreated);
    }

    [Fact]
    public async Task PreProcessor_RejectingBannedCustomer_CutsTheChain()
    {
        var sp = BuildServices();

        var result = await OrdersApi
            .Create(new OrderDto("BANNED-007", 100m, "USD"))
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsDomainFailure.Should().BeTrue();
        result.Match(_ => "ok", errs => errs[0].Code).Should().Be("CUSTOMER_BANNED");
        // No events should have been produced — pre-processor blocked Create before aggregate method ran.
        result.ProducedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DomainFailure_FromAggregateMethod_TerminatesWithValidationError()
    {
        var sp = BuildServices();

        var result = await OrdersApi
            .Create(new OrderDto("", 100m, "USD"))  // empty customerId — Create fails
            .ExecuteAsync(sp);

        result.IsDomainFailure.Should().BeTrue();
        var error = result.Match(
            success: _ => (Error)new ValidationError("unexpected", "chain should have failed", ""),
            failure: errs => errs[0]);
        error.Should().BeOfType<ValidationError>()
            .Which.Code.Should().Be("ORDER_CUSTOMER_REQUIRED");
    }

    [Fact]
    public async Task RepeatedChainBuilds_AreIndependent()
    {
        var sp = BuildServices();

        // Two chains built and executed back-to-back — each gets its own OrderId.
        var first = await OrdersApi.Create(ValidDto("C-A")).ExecuteAsync(sp);
        var second = await OrdersApi.Create(ValidDto("C-B")).ExecuteAsync(sp);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        var firstId = ((OrderCreated)first.ProducedEvents[0]).OrderId;
        var secondId = ((OrderCreated)second.ProducedEvents[0]).OrderId;
        firstId.Should().NotBe(secondId);
    }

    [Fact]
    public async Task ProducedEvents_DrainCallback_ObservesEventsBeforeChainCompletes()
    {
        var sp = BuildServices();
        var observed = new List<string>();

        var result = await OrdersApi
            .Create(ValidDto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .ProducedEvents(events =>
            {
                foreach (var e in events) observed.Add(e.GetType().Name);
            })
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        // Drain ran after PlaceOrder, so it saw Create + Place but not Inventory.
        observed.Should().Equal(nameof(OrderCreated), nameof(OrderPlaced));
        // Final ProducedEvents only contains what arrived after the drain.
        result.ProducedEvents.Select(e => e.GetType()).Should().Equal(typeof(OrderInventoryUpdated));
    }

    [Fact]
    public async Task OnError_FiresOnDomainFailure_WithExactErrorList()
    {
        var sp = BuildServices();
        IReadOnlyList<Error>? captured = null;

        var result = await OrdersApi
            .Create(new OrderDto("", 100m, "USD"))
            .OnError(errs => captured = errs)
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .ExecuteAsync(sp);

        result.IsDomainFailure.Should().BeTrue();
        captured.Should().NotBeNull();
        captured![0].Code.Should().Be("ORDER_CUSTOMER_REQUIRED");
    }

    [Fact]
    public async Task DispatchEvents_TriggersDomainEventHandlersForRaisedEvents()
    {
        var captured = new List<OrderPlaced>();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();
        services.AddOrderAggregate();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddCrucibleEventHandler<OrderPlaced, RecordingPlacedHandler>();
        services.AddSingleton(captured);
        var sp = services.BuildServiceProvider();

        var result = await OrdersApi
            .Create(ValidDto())
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .DispatchEvents()
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        captured.Should().ContainSingle()
            .Which.Carrier.Should().Be("UPS");
    }

    private sealed class RecordingPlacedHandler : IDomainEventHandler<OrderPlaced>
    {
        private readonly List<OrderPlaced> _captured;
        public RecordingPlacedHandler(List<OrderPlaced> captured) => _captured = captured;
        public Task HandleAsync(OrderPlaced @event, CancellationToken ct)
        {
            _captured.Add(@event);
            return Task.CompletedTask;
        }
    }

    // Simulates a placement service that rejects the order after Create succeeds,
    // used to prove events from earlier steps survive a handler failure.
    private sealed class FailingPlaceOrderHandler : IStepHandler<Order, OrderId, ShippingOptions, OrderPlaced>
    {
        public Task<Result> InvokeAsync(Order aggregate, ShippingOptions input, OrderPlaced stepResult, CancellationToken ct)
            => Task.FromResult(Result.Failure(new BusinessRuleError("PLACEMENT_REJECTED", "External placement rejected")));
    }
}
