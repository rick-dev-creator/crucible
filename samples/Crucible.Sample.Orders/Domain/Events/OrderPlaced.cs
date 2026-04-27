using Crucible.Domain.Events;

namespace Crucible.Sample.Orders.Domain.Events;

public sealed record OrderPlaced(OrderId OrderId, string Carrier) : DomainEvent;
