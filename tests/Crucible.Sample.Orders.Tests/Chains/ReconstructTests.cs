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
using Xunit;
using OrdersApi = Crucible.Sample.Orders.Domain.Orders;

namespace Crucible.Sample.Orders.Tests.Chains;

/// <summary>
/// Demonstrates Reconstruct: an external class (e.g., an EF entity) that
/// implements the generator-emitted I[Aggregate]Snapshot interface can be
/// passed directly to ReconstructAt[Step] to resume a chain at that phase.
/// No reflection involved — the generator emits a typed hydration helper
/// as a partial-class member.
/// </summary>
public sealed class ReconstructTests
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

    /// <summary>
    /// Stand-in for an EF Core entity loaded from the database. Implements
    /// the generated IOrderSnapshot interface. Outside Crucible's namespace,
    /// outside the aggregate's project — exactly how a real consumer would
    /// integrate persistence.
    /// </summary>
    private sealed class OrderRepositoryEntity : IOrderSnapshot
    {
        public OrderId Id { get; set; }
        public string CustomerId { get; set; } = "";
        public Money Total { get; set; } = Money.Zero("USD");
        public OrderStatus Status { get; set; }
        public string? Carrier { get; set; }
        public long Version { get; set; }
        public IReadOnlyList<IOrderItemSnapshot> Items { get; set; } = System.Array.Empty<IOrderItemSnapshot>();
    }

    [Fact]
    public async Task ReconstructAtPlaceOrder_FromExternalEntity_ResumesChainAtUpdateStep()
    {
        var sp = BuildServices();

        // A persisted order in Placed status, ready for inventory update.
        var entity = new OrderRepositoryEntity
        {
            Id = OrderId.From(System.Guid.NewGuid()),
            CustomerId = "C-100",
            Total = Money.Create(250m, "USD").Value,
            Status = OrderStatus.Placed,
            Carrier = "UPS",
            Version = 5,
        };

        var result = await OrdersApi
            .ReconstructAtPlaceOrder(entity)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        var updated = result.ProducedEvents.OfType<OrderInventoryUpdated>().Single();
        updated.OrderId.Should().Be(entity.Id);
    }

    [Fact]
    public async Task ReconstructAtCreate_FromExternalEntity_AllowsPlaceOrderNext()
    {
        var sp = BuildServices();

        var entity = new OrderRepositoryEntity
        {
            Id = OrderId.From(System.Guid.NewGuid()),
            CustomerId = "C-200",
            Total = Money.Create(50m, "EUR").Value,
            Status = OrderStatus.Draft,
            Version = 1,
        };

        var result = await OrdersApi
            .ReconstructAtCreate(entity)
            .PlaceOrder(new ShippingOptions("DHL", 3))
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        result.ProducedEvents.OfType<OrderPlaced>().Single().Carrier.Should().Be("DHL");
    }

    [Fact]
    public async Task ReconstructAtPlaceOrder_OnDraftSnapshot_FailsAtNextDomainStep()
    {
        // The snapshot says Draft, but we're entering at PlaceOrder phase.
        // The aggregate's UpdateOrderInventory check sees Status==Draft and rejects.
        // Demonstrates that domain invariants survive misuse of Reconstruct.
        var sp = BuildServices();

        var entity = new OrderRepositoryEntity
        {
            Id = OrderId.From(System.Guid.NewGuid()),
            CustomerId = "C-300",
            Total = Money.Create(10m, "USD").Value,
            Status = OrderStatus.Draft,  // intentionally inconsistent with phase
            Version = 1,
        };

        var result = await OrdersApi
            .ReconstructAtPlaceOrder(entity)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsDomainFailure.Should().BeTrue();
        result.Match(_ => "ok", errs => errs[0].ErrorCode).Should().Be("ORDER_NOT_PLACED");
    }
}
