using Crucible.Chains.Processors;
using Crucible.Domain.Errors;
using Crucible.Domain.Results;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;

namespace Crucible.Sample.Orders.Processors;

public sealed class ValidateCustomerPreProcessor : IPreProcessor<Order, OrderId, OrderDto>
{
    public Task<Result> InvokeAsync(PreContext<Order, OrderId, OrderDto> context, CancellationToken ct)
    {
        if (context.Input.CustomerId.StartsWith("BANNED-", StringComparison.Ordinal))
            return Task.FromResult(Result.Failure(new ConflictError("CUSTOMER_BANNED", "Customer is on the banned list")));
        return Task.FromResult(Result.Success());
    }
}
