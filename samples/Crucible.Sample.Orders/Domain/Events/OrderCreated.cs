using Crucible.Domain.Events;

namespace Crucible.Sample.Orders.Domain.Events;

public sealed record OrderCreated(OrderId OrderId, string CustomerId, Money Total) : DomainEvent;
