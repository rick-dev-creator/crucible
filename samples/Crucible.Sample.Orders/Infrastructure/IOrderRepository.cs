using Crucible.Sample.Orders.Domain;

namespace Crucible.Sample.Orders.Infrastructure;

public interface IOrderRepository
{
    Task SaveAsync(Order order, CancellationToken ct);
    Task<Order?> FindAsync(OrderId id, CancellationToken ct);
}
