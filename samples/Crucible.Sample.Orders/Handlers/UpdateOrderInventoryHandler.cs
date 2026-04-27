using Crucible.Chains.Handlers;
using Crucible.Chains.Steps;
using Crucible.Domain.Results;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.Infrastructure;

namespace Crucible.Sample.Orders.Handlers;

public sealed class UpdateOrderInventoryHandler : IStepHandler<Order, OrderId, Unit, OrderInventoryUpdated>
{
    private readonly IOrderRepository _repo;
    public UpdateOrderInventoryHandler(IOrderRepository repo) => _repo = repo;

    public async Task<Result> InvokeAsync(Order aggregate, Unit input, OrderInventoryUpdated stepResult, CancellationToken ct)
    {
        await _repo.SaveAsync(aggregate, ct);
        return Result.Success();
    }
}
