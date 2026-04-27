using Crucible.Chains.DependencyInjection;
using Crucible.Chains.Results;
using Crucible.Domain.Errors;
using Crucible.Sample.Orders.Domain;
using Crucible.Sample.Orders.Domain.Dtos;
using Crucible.Sample.Orders.Domain.Events;
using Crucible.Sample.Orders.EventHandlers;
using Crucible.Sample.Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCrucible();
builder.Services.AddOrderAggregate();
builder.Services.AddCrucibleEventHandler<OrderPlaced, NotifyWarehouseHandler>();
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

var app = builder.Build();

app.MapPost("/orders", async (
    OrderDto dto,
    string carrier,
    int priorityDays,
    IServiceProvider sp,
    CancellationToken ct) =>
{
    return await Orders
        .Create(dto)
        .PlaceOrder(new ShippingOptions(carrier, priorityDays))
        .UpdateOrderInventory()
        .DispatchEvents()
        .ExecuteAsync(sp, ct)
        .Catch(ex => new Error[] { new InfrastructureError("ORDER_PIPELINE", ex.Message) })
        .Match(
            success => Results.Ok(new { success.OrderId, success.OccurredAt }),
            errors  => Results.BadRequest(new { errors }));
});

app.Run();

public partial class Program { }
