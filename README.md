# Crucible

Opinionated DDD library for .NET 10 / C# 14. Forces aggregates, typestate-restricted fluent chains, and explicit error handling so LLMs and junior developers cannot fragment the domain.

## Why

Building business systems with LLM collaborators (and junior devs) suffers predictable structural decay: 800-line god services, validation duplicated in 5 layers, events emitted from any layer, handlers calling handlers, silent fallbacks that mask bugs. Crucible's thesis: **make incorrect code uncompilable**.

Each Crucible layer owns exactly one failure mode:
- Aggregate methods → business rule failures (`Result<T>`)
- Step handlers → infrastructure failures (`.Catch()`)
- Domain event handlers → post-commit side effects (policy)
- Source-generated typestate → composition failures (compile errors)

## Hello World

```csharp
[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    [Step(Order = 1, Entry = true)]
    public Result<OrderCreated> Create(OrderDto dto) { /* validate, mutate, Raise(...) */ }

    [Step(Order = 2)]
    public Result<OrderPlaced> PlaceOrder(ShippingOptions s) { /* ... */ }

    [Step(Order = 3)]
    public Result<OrderInventoryUpdated> UpdateOrderInventory() { /* ... */ }
}

// Endpoint:
return await Orders
    .Create(dto)
    .PlaceOrder(shipping)
    .UpdateOrderInventory()
    .DispatchEvents()
    .ExecuteAsync(sp, ct)
    .Catch(ex => new Error[] { new InfrastructureError("X", ex.Message) })
    .Match(
        success => Results.Ok(success),
        errors  => Results.BadRequest(errors));
```

## Install

Three packages — typically all referenced together:

```xml
<PackageReference Include="Crucible.Domain" Version="1.0.0" />
<PackageReference Include="Crucible.Chains" Version="1.0.0" />
<PackageReference Include="Crucible.Generators" Version="1.0.0" PrivateAssets="all" OutputItemType="Analyzer" />
```

## Setup

```csharp
services.AddCrucible(opts => opts.AddBehavior<TracingBehavior>());
services.AddOrderAggregate();
services.AddCrucibleEventHandler<OrderPlaced, NotifyWarehouseHandler>();
```

## Diagnostics

| Code | Severity | Meaning |
|---|---|---|
| CRC001 | Error | `[Aggregate]` has no `Entry = true` step |
| CRC002 | Error | `[Aggregate]` has multiple `Entry = true` steps |
| CRC003 | Error | Duplicate `[Step(Order = N)]` |
| CRC004 | Error | Gap in `[Step(Order = N)]` sequence |
| CRC005 | Error | `[Aggregate]` is not `partial` |
| CRC006 | Error | `[Aggregate]` does not derive from `AggregateRoot<TId>` |
| CRC007 | Error | `[Step]` returns something other than `Result<T>` or `Result` |
| CRC008 | Error | `[Step]` is `async` or returns `Task` |
| CRC010 | Error | Multiple handler implementations match the same step |
| CRC100 | Info | Step has no handler — runs as domain-only |
| CRC200 | Warning | `[Pre<T>]` / `[Post<T>]` target does not implement the expected interface |

## Out of scope (v1.0)

Persistence, queries / read models, event sourcing, cross-aggregate transactions, branching typestate. See [roadmap](docs/superpowers/specs/2026-04-27-crucible-design.md#11-roadmap-post-v1).

## License

MIT.
