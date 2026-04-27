using Crucible.Domain.Events;
using Crucible.Sample.Orders.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Crucible.Sample.Orders.EventHandlers;

public sealed class NotifyWarehouseHandler : IDomainEventHandler<OrderPlaced>
{
    private readonly ILogger<NotifyWarehouseHandler> _log;
    public NotifyWarehouseHandler(ILogger<NotifyWarehouseHandler> log) => _log = log;

    public Task HandleAsync(OrderPlaced @event, CancellationToken ct)
    {
        _log.LogInformation("Notifying warehouse for order {OrderId} via {Carrier}", @event.OrderId, @event.Carrier);
        return Task.CompletedTask;
    }
}
