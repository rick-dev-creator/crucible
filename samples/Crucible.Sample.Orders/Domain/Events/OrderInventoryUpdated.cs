using Crucible.Domain.Events;

namespace Crucible.Sample.Orders.Domain.Events;

public sealed record OrderInventoryUpdated(OrderId OrderId) : DomainEvent;
