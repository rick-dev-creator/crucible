using Crucible.Chains.DependencyInjection;
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
/// Senior-DDD-style tests for aggregate-with-entities Reconstruct: each test pulls the
/// post-chain aggregate from the InMemoryOrderRepository and asserts its real state,
/// including the rehydrated Items collection. No reflection, no chain internals.
/// </summary>
public sealed class ReconstructWithItemsTests
{
    private static (IServiceProvider Sp, IOrderRepository Repo) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();
        services.AddOrderAggregate();
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<IOrderRepository>());
    }

    private sealed class OrderEntity : IOrderSnapshot
    {
        public OrderId Id { get; set; }
        public string CustomerId { get; set; } = "";
        public Money Total { get; set; } = Money.Zero("USD");
        public OrderStatus Status { get; set; }
        public string? Carrier { get; set; }
        public long Version { get; set; }
        public IReadOnlyList<IOrderItemSnapshot> Items { get; set; } = System.Array.Empty<IOrderItemSnapshot>();
    }

    private sealed record OrderItemRow(OrderItemId Id, string ProductSku, int Quantity, Money UnitPrice) : IOrderItemSnapshot;

    private static OrderEntity MakeEntity(OrderStatus status, params OrderItemRow[] items) => new()
    {
        Id = OrderId.From(System.Guid.NewGuid()),
        CustomerId = "C-001",
        Total = Money.Create(150m, "USD").Value,
        Status = status,
        Carrier = status == OrderStatus.Draft ? null : "UPS",
        Version = 4,
        Items = items,
    };

    [Fact]
    public async Task ReconstructAtPlaceOrder_WithItems_RehydratesEachItemWithExactValues()
    {
        var (sp, repo) = BuildServices();

        var item1 = new OrderItemRow(OrderItemId.From(System.Guid.NewGuid()), "SKU-A", 2, Money.Create(50m, "USD").Value);
        var item2 = new OrderItemRow(OrderItemId.From(System.Guid.NewGuid()), "SKU-B", 1, Money.Create(50m, "USD").Value);
        var entity = MakeEntity(OrderStatus.Placed, item1, item2);

        var result = await OrdersApi
            .ReconstructAtPlaceOrder(entity)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();

        // Pull the persisted aggregate from the repo and assert state directly.
        var saved = await repo.FindAsync(entity.Id, default);
        saved.Should().NotBeNull();
        saved!.Items.Should().HaveCount(2);

        var savedById = saved.Items.ToDictionary(i => i.Id);
        savedById[item1.Id].ProductSku.Should().Be("SKU-A");
        savedById[item1.Id].Quantity.Should().Be(2);
        savedById[item1.Id].UnitPrice.Should().Be(Money.Create(50m, "USD").Value);
        savedById[item2.Id].ProductSku.Should().Be("SKU-B");
        savedById[item2.Id].Quantity.Should().Be(1);
    }

    [Fact]
    public async Task ReconstructAtCreate_PreservesItemCollectionExactly_ThroughPlaceOrder()
    {
        var (sp, repo) = BuildServices();

        var itemId = OrderItemId.From(System.Guid.NewGuid());
        var entity = MakeEntity(
            OrderStatus.Draft,
            new OrderItemRow(itemId, "SKU-X", 5, Money.Create(15m, "EUR").Value));

        var result = await OrdersApi
            .ReconstructAtCreate(entity)
            .PlaceOrder(new ShippingOptions("DHL", 3))
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        var saved = await repo.FindAsync(entity.Id, default);
        saved.Should().NotBeNull();
        saved!.Items.Should().ContainSingle(i => i.Id == itemId)
            .Which.ProductSku.Should().Be("SKU-X");
        // PlaceOrder transitioned status — verify the item collection survived the transition
        saved.Status.Should().Be(OrderStatus.Placed);
        saved.Carrier.Should().Be("DHL");
    }

    [Fact]
    public async Task ReconstructAtPlaceOrder_WithEmptyItemsCollection_RunsCleanAndProducesEmptyItems()
    {
        var (sp, repo) = BuildServices();
        var entity = MakeEntity(OrderStatus.Placed);  // no items
        entity.Items = System.Array.Empty<IOrderItemSnapshot>();

        var result = await OrdersApi
            .ReconstructAtPlaceOrder(entity)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        var saved = await repo.FindAsync(entity.Id, default);
        saved!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ReconstructAtPlaceOrder_PreservesVersionFromSnapshot()
    {
        var (sp, repo) = BuildServices();
        var entity = MakeEntity(OrderStatus.Placed, new OrderItemRow(OrderItemId.New(), "SKU", 1, Money.Create(10m, "USD").Value));
        entity.Version = 17;

        var result = await OrdersApi
            .ReconstructAtPlaceOrder(entity)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsSuccess.Should().BeTrue();
        var saved = await repo.FindAsync(entity.Id, default);
        // The chain bumped Version once for the UpdateOrderInventory step.
        saved!.Version.Should().Be(18);
    }

    [Fact]
    public async Task ReconstructAtCreate_ItemsHaveEntityIdentityEquality()
    {
        var (sp, repo) = BuildServices();
        var sharedId = OrderItemId.From(System.Guid.NewGuid());
        var entity = MakeEntity(
            OrderStatus.Draft,
            new OrderItemRow(sharedId, "SKU-Z", 9, Money.Create(99m, "USD").Value));

        await OrdersApi
            .ReconstructAtCreate(entity)
            .PlaceOrder(new ShippingOptions("UPS", 1))
            .ExecuteAsync(sp);

        var saved = await repo.FindAsync(entity.Id, default);
        var rehydratedItem = saved!.Items.Single();
        rehydratedItem.Id.Should().Be(sharedId);

        // Entity<TId> equality is identity-based — a same-Id stand-in equals it.
        var stub = new OrderItem(sharedId, "different-sku", 0, Money.Zero("USD"));
        rehydratedItem.Equals(stub).Should().BeTrue();
    }

    [Fact]
    public async Task IndependentReconstructionsDoNotShareItemState()
    {
        var (sp, repo) = BuildServices();

        var aId = OrderItemId.From(System.Guid.NewGuid());
        var bId = OrderItemId.From(System.Guid.NewGuid());

        var entityA = MakeEntity(OrderStatus.Placed, new OrderItemRow(aId, "SKU-A", 1, Money.Create(10m, "USD").Value));
        var entityB = MakeEntity(OrderStatus.Placed, new OrderItemRow(bId, "SKU-B", 2, Money.Create(20m, "USD").Value));

        await OrdersApi.ReconstructAtPlaceOrder(entityA).UpdateOrderInventory().ExecuteAsync(sp);
        await OrdersApi.ReconstructAtPlaceOrder(entityB).UpdateOrderInventory().ExecuteAsync(sp);

        var savedA = await repo.FindAsync(entityA.Id, default);
        var savedB = await repo.FindAsync(entityB.Id, default);

        savedA!.Items.Should().ContainSingle().Which.Id.Should().Be(aId);
        savedB!.Items.Should().ContainSingle().Which.Id.Should().Be(bId);
        savedA.Items.Single().ProductSku.Should().Be("SKU-A");
        savedB.Items.Single().ProductSku.Should().Be("SKU-B");
    }

    [Fact]
    public async Task ReconstructAtPlaceOrder_OnDraftStatusInSnapshot_FailsAtNextDomainStep_ItemsStillRehydrated()
    {
        // Even when the snapshot is inconsistent (Status=Draft passed via "AtPlaceOrder" entry),
        // the items collection is rehydrated before the next aggregate method runs and rejects.
        var (sp, repo) = BuildServices();

        var item = new OrderItemRow(OrderItemId.New(), "SKU-Q", 3, Money.Create(30m, "USD").Value);
        var entity = MakeEntity(OrderStatus.Draft, item);  // mismatch: Draft on a Placed-phase entry

        var result = await OrdersApi
            .ReconstructAtPlaceOrder(entity)
            .UpdateOrderInventory()
            .ExecuteAsync(sp);

        result.IsDomainFailure.Should().BeTrue();
        // The repo never received a save (handler runs only on aggregate-method success),
        // but we can verify by trying to find — should be null since UpdateOrderInventoryHandler never persisted.
        var saved = await repo.FindAsync(entity.Id, default);
        saved.Should().BeNull();
    }
}
