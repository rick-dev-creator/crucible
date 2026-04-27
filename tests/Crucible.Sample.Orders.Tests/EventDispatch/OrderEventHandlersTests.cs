using Crucible.Chains.DependencyInjection;
using Crucible.Domain.Events;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.Infrastructure;
using FluentAssertions;
using OrdersApi = Crucible.Sample.Orders.Domain.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Crucible.Sample.Orders.Tests.EventDispatch;

public sealed class OrderEventHandlersTests
{
    [Fact]
    public async Task MultipleHandlersForSameEvent_AllFire_InRegistrationOrder()
    {
        var log = new List<string>();
        var services = BuildBaseServices(log);
        services.AddCrucibleEventHandler<OrderPlaced, RecordingHandler>();
        services.AddCrucibleEventHandler<OrderPlaced, AuditHandler>();
        var sp = services.BuildServiceProvider();

        await OrdersApi
            .Create(new OrderDto("C-1", 100m, "USD"))
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .DispatchEvents()
            .ExecuteAsync(sp);

        log.Should().Equal("RecordingHandler", "AuditHandler");
    }

    [Fact]
    public async Task HandlerThrows_WithLogAndContinuePolicy_ChainStillSucceeds()
    {
        var log = new List<string>();
        var services = BuildBaseServices(log);
        services.AddCrucibleEventHandler<OrderPlaced, ThrowingHandler>();
        services.AddCrucibleEventHandler<OrderPlaced, RecordingHandler>();
        var sp = services.BuildServiceProvider();

        var result = await OrdersApi
            .Create(new OrderDto("C-1", 100m, "USD"))
            .PlaceOrder(new ShippingOptions("UPS", 2))
            .DispatchEvents()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        // ThrowingHandler logs but doesn't break the chain; RecordingHandler still fires.
        log.Should().Contain("RecordingHandler");
    }

    private static ServiceCollection BuildBaseServices(List<string> log)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();  // default: LogAndContinue
        services.AddOrderAggregate();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton(log);
        return services;
    }

    private sealed class RecordingHandler : IDomainEventHandler<OrderPlaced>
    {
        private readonly List<string> _log;
        public RecordingHandler(List<string> log) => _log = log;
        public Task HandleAsync(OrderPlaced @event, CancellationToken ct)
        {
            _log.Add(nameof(RecordingHandler));
            return Task.CompletedTask;
        }
    }

    private sealed class AuditHandler : IDomainEventHandler<OrderPlaced>
    {
        private readonly List<string> _log;
        public AuditHandler(List<string> log) => _log = log;
        public Task HandleAsync(OrderPlaced @event, CancellationToken ct)
        {
            _log.Add(nameof(AuditHandler));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IDomainEventHandler<OrderPlaced>
    {
        public Task HandleAsync(OrderPlaced @event, CancellationToken ct)
            => throw new System.InvalidOperationException("warehouse dead");
    }
}
