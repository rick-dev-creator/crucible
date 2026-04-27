using Crucible.Chains.Handlers;
using Crucible.Domain.Results;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.Infrastructure;

namespace Crucible.Sample.Orders.Handlers;

public sealed class PlaceOrderHandler : IStepHandler<Order, OrderId, ShippingOptions, OrderPlaced>
{
    private readonly IOrderRepository _repo;
    public PlaceOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<Result> InvokeAsync(Order aggregate, ShippingOptions input, OrderPlaced stepResult, CancellationToken ct)
    {
        await _repo.SaveAsync(aggregate, ct);
        return Result.Success();
    }
}
