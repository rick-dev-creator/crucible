using System.Collections.Concurrent;
using Crucible.Sample.Orders.Domain;

namespace Crucible.Sample.Orders.Infrastructure;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<Guid, Order> _store = new();

    public Task SaveAsync(Order order, CancellationToken ct)
    {
        _store[order.Id.Value] = order;
        return Task.CompletedTask;
    }

    public Task<Order?> FindAsync(OrderId id, CancellationToken ct)
        => Task.FromResult(_store.TryGetValue(id.Value, out var o) ? o : null);
}
