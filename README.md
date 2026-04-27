# Crucible

Opinionated DDD library for .NET 10 / C# 14. Forces aggregates, entities, value objects, and chain composition through compile-time constraints so LLMs and junior developers cannot fragment the domain.

> v2.2.0 — production-ready core (aggregates, entities, value objects, chain runtime, branching typestate, EF Core-friendly Reconstruct/Snapshot, IError contract). 144 tests passing.

---

## The problem this solves

Building a non-trivial business system with LLM collaborators or rotating junior developers tends to fall into the same structural traps every time:

- A `OrderService` that grows past 800 lines, doing validation, persistence, event publishing, and orchestration in one mega class.
- The same validation rule duplicated in 5 places — controller, service, validator, aggregate, handler — each subtly different. Bugs ship when one drifts.
- Domain events raised from controllers, handlers, even from inside `try/catch` blocks. Nobody knows where state actually changes.
- Step handlers calling other step handlers calling other step handlers, until a single command fans out into 30 method invocations across 12 files.
- Silent fallbacks: a method returns `null` or an empty list when something fails, the caller doesn't notice, and a real bug ships to production.
- User-facing copy ("The customer was not found, please try again") embedded in domain return values, blocking future i18n and coupling business code to UI concerns.
- Public constructors on aggregates and entities, letting any code instantiate a domain object that bypasses validation rules.
- Workflow logic ("you can `Approve` only if status is `Draft`") expressed as runtime `if` checks in every method, easy to forget and impossible to enforce when a new method is added.

Each of these is fixable in code review. None is fixable in code review *every time* on a project that runs for months with a team that turns over and an AI that produces 60% of the code.

---

## What Crucible does

Crucible makes those failure modes **uncompilable**. Not "discouraged in a style guide" — uncompilable.

It is a library + Roslyn source generator that, given a few attributes on your domain types (`[Aggregate]`, `[Entity]`, `[ValueObject]`, `[Step]`), produces:

1. **Typestate-restricted fluent chains.** A consumer writing `Orders.Create(dto).PlaceOrder(shipping)` is constrained at compile time to a valid sequence of methods. If your aggregate's workflow is `Create → Place → UpdateInventory`, calling `UpdateInventory()` directly after `Create()` does not compile — the method is not available on that type. Branching workflows (Approve|Reject) get the same enforcement as a DAG.
2. **Domain methods gate handlers.** Each step is two phases: an aggregate method (synchronous, no I/O) followed by its handler (async, I/O). The handler runs only if the aggregate method returned `Result.Success`. Validation cannot be bypassed by the infrastructure side.
3. **No exceptions for business rules.** Domain methods return `Result<T>`. Match is exhaustive. Exceptions propagate as a separate `Exceptional` state, isolated through `.Catch()`. Each layer owns one failure mode.
4. **No user-facing messages in the domain.** Errors implement `IError` with `ErrorCode` and `ErrorDescription` (the latter explicitly named for internal logging, never for end-user presentation). Localized copy lives in the presentation layer.
5. **No invalid value objects in memory.** `[ValueObject]` types are constructed only through a generator-emitted `Create` factory that runs developer-defined validation; failure returns `Result<TVO>.Failure`, never an invalid instance.
6. **No bypassing the chain.** Aggregates and entities cannot have public constructors. The only way to instantiate a domain object is `Orders.Create(...)`, `Orders.ReconstructAt[Step](...)`, or aggregate-internal entity construction.
7. **No reflection at runtime.** The chain executor invokes generated step types directly. Every binding is explicit code emitted by the source generator and visible in `obj/Generated/`.
8. **EF Core friendly persistence.** Aggregates, entities, and value objects all have private parameterless constructors and `init` setters — exactly the shape EF Core 8+ uses for owned-type materialization. The `Reconstruct` flow lets a loaded EF entity be passed directly to resume a chain at any phase.

The whole thing is enforced through 27 Roslyn diagnostics (`CRC001`–`CRC404`). A team that adds a public constructor to an aggregate, returns a free-form `Message` instead of an `IError`, or composes chain methods in an invalid order doesn't get a code review comment. They get a build failure.

---

## Motivation

This technique is not new for me — I've used variations of it on internal projects for years. What's new is materializing it as an open-source library. Crucible is the consolidation of patterns that worked in those private codebases, packaged so other teams can adopt the parts they want.

The trigger for publishing was a CRM where, given enough rope, an LLM eventually fragmented the domain into a maze of services, validators, and partial implementations of the same business rule. Documentation didn't help; the LLM (and the juniors) didn't read it. Code review caught some of it; the rest shipped and bit us months later. The realization: **structural enforcement at compile time is the only durable defense**. Anything you "ask" the developer to do is something the developer (human or AI) will eventually skip. Anything that doesn't compile cannot ship.

Crucible is the structural enforcement encoded as a library. It is not friendlier than what you already have — it is more restrictive on purpose. The friction is the feature.

### Who this is *not* for

Crucible is opinionated. It encodes a specific reading of DDD that has worked well on internal projects, but DDD itself is interpreted differently by different teams. **This library is not a universal DDD framework**, and it is not for every project:

- If your team prefers anemic domain models with services orchestrating mutations: Crucible will fight you at every step. Don't use it.
- If your team prefers exceptions for domain rules: Crucible's `Result<T>` discipline will feel pedantic. Don't use it.
- If your team prefers public constructors and factory methods on aggregates: Crucible blocks that with CRC011/CRC305/CRC402. Don't use it.
- If your aggregates are mostly CRUD and you don't have multi-step domain workflows: the chain typestate is overkill. A simpler library or no library is better.
- If you do event sourcing today and need it as a first-class concept: Crucible v2.2 is state-based; ES is on the roadmap but not shipped.
- If you need cross-aggregate orchestration (sagas with compensation, long-running workflows): use MassTransit or Wolverine — Crucible doesn't aim to replace those.

There are common DDD principles (aggregate as transactional boundary, value objects with structural equality, domain events, ubiquitous language) that almost all DDD practitioners agree on. Crucible enforces these. Beyond that, it makes opinionated calls — and those calls won't suit every team. That's deliberate. A framework that tries to please everyone enforces nothing.

If after reading the diagnostics list you find yourself wanting to suppress half of them, Crucible is the wrong fit. If you read them and think "yes, exactly, my team should hit all of these", you're the audience.

---

## Hello world

A complete aggregate, with a domain event, a handler, a pre-processor, and an HTTP endpoint:

```csharp
// Domain — the developer writes this
[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private Order() { }                                          // CRC011 — must be private

    public string CustomerId { get; private set; } = "";
    public Money Total { get; private set; } = Money.Zero("USD");
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;

    [Step(Order = 1, Entry = true)]
    [Pre<ValidateCustomerPreProcessor>]
    public Result<OrderCreated> Create(OrderDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
            return new ValidationError("ORDER_CUSTOMER_REQUIRED", "CustomerId is required", nameof(dto.CustomerId));

        var totalResult = Money.Create(dto.Amount, dto.Currency);
        if (totalResult.IsFailure) return Result<OrderCreated>.Failure(totalResult.Errors);

        Id = OrderId.New();
        CustomerId = dto.CustomerId;
        Total = totalResult.Value;
        Status = OrderStatus.Draft;
        var evt = new OrderCreated(Id, CustomerId, Total);
        Raise(evt);
        return evt;
    }

    [Step(Order = 2)]
    public Result<OrderPlaced> PlaceOrder(ShippingOptions s)
    {
        if (Status != OrderStatus.Draft)
            return new BusinessRuleError("ORDER_NOT_DRAFT", "Order must be in Draft status to place");
        Status = OrderStatus.Placed;
        var evt = new OrderPlaced(Id, s.Carrier);
        Raise(evt);
        return evt;
    }
}

[ValueObject]
public sealed partial record Money : ValueObject
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "";

    private Money() { }                                         // CRC403 — required for hydration + EF

    private static partial Result __ValidateConstruction(decimal amount, string currency)
    {
        var errors = new List<IError>();
        if (amount < 0)
            errors.Add(new ValidationError("MONEY_NEGATIVE_AMOUNT", "Money amount must be non-negative", nameof(Amount)));
        if (string.IsNullOrWhiteSpace(currency))
            errors.Add(new ValidationError("MONEY_CURRENCY_REQUIRED", "Money currency is required", nameof(Currency)));
        return errors.Count > 0 ? Result.Failure(errors) : Result.Success();
    }
}

// Infrastructure — the developer writes this
public sealed class CreateOrderHandler : IStepHandler<Order, OrderId, OrderDto, OrderCreated>
{
    private readonly IOrderRepository _repo;
    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<Result> InvokeAsync(Order aggregate, OrderDto input, OrderCreated stepResult, CancellationToken ct)
    {
        await _repo.SaveAsync(aggregate, ct);
        return Result.Success();
    }
}

public sealed class ValidateCustomerPreProcessor : IPreProcessor<Order, OrderId, OrderDto>
{
    public Task<Result> InvokeAsync(PreContext<Order, OrderId, OrderDto> ctx, CancellationToken ct)
    {
        if (ctx.Input.CustomerId.StartsWith("BANNED-"))
            return Task.FromResult(Result.Failure(new ConflictError("CUSTOMER_BANNED", "Customer is on the banned list")));
        return Task.FromResult(Result.Success());
    }
}

// Endpoint — the consumer writes this. Note the chain syntax: typestate enforces order at compile time.
app.MapPost("/orders", async (OrderDto dto, string carrier, IServiceProvider sp, CancellationToken ct) =>
{
    return await Orders
        .Create(dto)
        .PlaceOrder(new ShippingOptions(carrier, 2))
        .DispatchEvents()                                                         // appends a chain step
        .ExecuteAsync(sp, ct)                                                     // <-- here the plan runs
        .Catch(ex => new IError[] { new InfrastructureError("ORDER_PIPELINE", ex.Message) })
        .Match(
            success => Results.Ok(new { success.OrderId, success.OccurredAt }),
            errors  => Results.BadRequest(new { errors }));
});
```

> **The chain is deferred.** Every method before `ExecuteAsync` (including `DispatchEvents`, `Tap`, `OnError`, `ProducedEvents`) just appends a step to the plan — none of them execute domain logic at the call site. `ExecuteAsync` runs the assembled plan in order. So in the example above, `DispatchEvents` appearing syntactically before `ExecuteAsync` does **not** mean events fire before the chain runs; it means "as a step in this plan, drain accumulated events and dispatch them to `IDomainEventHandler<T>` handlers." That step executes when the chain reaches it — typically last, so events fire only after every prior step succeeded. Mid-chain placement is also valid (e.g., dispatch before a later step that depends on the side effect). Same model as LINQ: `.Where().Select()` is just a plan; `.ToList()` runs it.

Things you cannot do with this code (and the diagnostic that fires):

| Attempted misuse | Diagnostic / compile error |
|---|---|
| `new Order()` outside the chain | CRC011 — `Aggregate` ctor is private |
| `new Money(100m, "USD")` directly | CRC402 — `ValueObject` ctor is private; use `Money.Create` |
| `Orders.PlaceOrder(...)` without `Create` first | Compile error — `PlaceOrder` not on starting type |
| `Orders.Create(dto).UpdateInventory()` skipping `PlaceOrder` | Compile error — typestate restricts available methods |
| Returning `string Message` from a domain method | The library forces `IError` — there is no string return path |
| `throw new BusinessRuleException(...)` from a domain method | No such exception type; business rules go through `Result.Failure` |
| Adding a public ctor to `Order` for testing | CRC011 — same enforcement applies in tests |

Tests use `InternalsVisibleTo` to construct domain objects, or — preferably — go through the chain itself.

---

## How it fits into your stack

Crucible is a **domain engine**. It does not replace your CQRS / mediator / HTTP layer. It lives *inside* your command handler:

```
HTTP / gRPC / queue consumer
       │
       ▼
Application layer (MediatR, Wolverine, raw service classes — your choice)
       │
       ▼
[CommandHandler.Handle calls Crucible chain]   ← Crucible operates here
       │
       ▼
Persistence (EF Core, Marten, Dapper — your choice)
```

A typical MediatR handler using Crucible:

```csharp
public sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, IResult>
{
    private readonly IServiceProvider _sp;
    public PlaceOrderCommandHandler(IServiceProvider sp) => _sp = sp;

    public async Task<IResult> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        return await Orders
            .Create(cmd.ToOrderDto())
            .PlaceOrder(cmd.ShippingOptions)
            .DispatchEvents()
            .ExecuteAsync(_sp, ct)
            .Match(
                success => Results.Ok(success),
                errors  => Results.BadRequest(errors));
    }
}
```

For queries (`GetOrderById`, `ListOrdersByCustomer`), Crucible is intentionally absent — queries don't go through aggregates. Use whatever you prefer (Dapper, EF Core query, MediatR query handler).

---

## Core concepts

### `[Aggregate]`

The transactional boundary of your domain. A class with this attribute:
- Must be `partial` and inherit `AggregateRoot<TId>` (CRC005, CRC006).
- Must have a `private` parameterless constructor (CRC011 if any ctor is public).
- Has one or more `[Step]` methods, exactly one marked `Entry = true` (CRC001, CRC002).
- The generator emits a static entry class (`Orders` for `Order`) with the entry method, plus per-step extension methods that gate composition through typestate.

```csharp
[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private Order() { }
    [Step(Order = 1, Entry = true)] public Result<OrderCreated> Create(OrderDto dto) { ... }
    [Step(Order = 2)] public Result<OrderPlaced> Place(ShippingOptions s) { ... }
}
```

### `[Entity]`

A child object inside an aggregate. Has identity (Id-based equality) but no chain entry of its own.
- Must be `partial`, derive from `Entity<TId>`, have a `private` parameterless constructor (CRC300–305).
- The generator emits `IEntitySnapshot` + `__HydrateFromSnapshot` + a `static RehydrateFrom` factory.
- Aggregates that hold `IReadOnlyList<TEntity>` properties get the children automatically referenced in the aggregate's snapshot interface.

```csharp
[Entity]
public partial class OrderItem : Entity<OrderItemId>
{
    public string ProductSku { get; private set; } = "";
    public int Quantity { get; private set; }
    private OrderItem() { }
    internal OrderItem(OrderItemId id, string sku, int qty) { Id = id; ProductSku = sku; Quantity = qty; }
}
```

### `[ValueObject]`

Identity-less, immutable, structurally compared. Construction is forced through a generator-emitted `Create` factory.
- Must be `sealed partial record`, derive from `ValueObject`, have a `private` parameterless constructor, all properties `init`-only (CRC400–404).
- The generator emits `public static Result<TVO> Create(...)` calling a developer-provided `static partial Result __ValidateConstruction(...)`.

```csharp
[ValueObject]
public sealed partial record Money : ValueObject
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "";
    private Money() { }

    private static partial Result __ValidateConstruction(decimal amount, string currency) { ... }
}

var moneyResult = Money.Create(100m, "USD");
```

### `Result<T>` and `IError`

Domain methods return `Result<T>`. Errors are `IError` instances:

```csharp
public interface IError
{
    string ErrorCode { get; }            // stable, machine-readable
    string ErrorDescription { get; }     // for internal logging only — NOT user-facing
    ErrorKind Kind { get; }
}
```

Built-in implementations: `ValidationError`, `BusinessRuleError`, `ConflictError`, `NotFoundError`, `InfrastructureError`. Custom: implement `IError` directly or extend the abstract `Error` record.

`Result<T>` and `ChainResult<T>` are exhaustive: `Match(success, failure)` covers both branches. There is no path that silently ignores errors.

### Chain runtime

Each `[Step]` method on an aggregate becomes a node in a fluent chain. **The chain is deferred:** each method call (`Create`, `PlaceOrder`, `Tap`, `OnError`, `ProducedEvents`, `DispatchEvents`) only appends a step to the plan. `ExecuteAsync` actually runs the assembled plan, in order, and returns `Task<ChainResult<TFinalState>>`. Same mental model as LINQ — until you reach the terminal call, you're describing a pipeline, not running one.

Per aggregate-method step the executor runs (in order):
1. `IStepBehavior` decorators (cross-cutting concerns: tracing, logging, metrics)
2. `[Pre<TPre>]` processors (validation, authorization)
3. The aggregate method itself (synchronous, returns `Result<TOutput>`)
4. The `IStepHandler<...>` for the step (async, returns `Result`; only runs if aggregate returned success)
5. `[Post<TPost>]` processors (telemetry, fire-and-forget side effects)
6. Pending events from the aggregate are drained into the chain's accumulated event log

`DispatchEvents`, `Tap`, `OnError`, `ProducedEvents` are also chain steps. They execute when the plan reaches them — not at the call site, not at `ExecuteAsync` invocation. Their placement in the chain is meaningful:

```csharp
// (a) Dispatch at the end — events fire only after every prior step succeeded:
.Create(dto).PlaceOrder(s).UpdateOrderInventory().DispatchEvents().ExecuteAsync(sp, ct);

// (b) Dispatch mid-chain — events from Create fire before PlaceOrder runs (useful when
//     a later step depends on the side effect being already published):
.Create(dto).DispatchEvents().PlaceOrder(s).ExecuteAsync(sp, ct);

// (c) ProducedEvents as inspection without drain — useful for logging/checkpointing:
.Create(dto)
.ProducedEvents(events => log.LogInfo("after Create: {Count} events", events.Count), drain: false)
.PlaceOrder(s).DispatchEvents().ExecuteAsync(sp, ct);
```

After execution, `ChainResult<T>` exposes three states (`Success`, `DomainFailure`, `Exceptional`) and the cumulative event log. `Match` is exhaustive over success/failure; `Catch` translates exceptions into `IError`s before `Match` sees the result:

```csharp
return await Orders
    .Create(dto)
    .PlaceOrder(shipping)
    .UpdateOrderInventory()
    .DispatchEvents()                                  // chain step — event handlers fire here, in plan order
    .ExecuteAsync(sp, ct)                              // <-- plan runs from this point
    .Catch(ex => /* exception → IError[] */)           // post-execute (Task<ChainResult<T>> extension)
    .Match(                                            // post-execute, terminal
        success => Results.Ok(success),
        errors  => Results.BadRequest(errors));
```

### Branching workflows

When the workflow is non-linear (approve/reject, authorize/void, etc.), declare predecessors with `AllowedAfter`:

```csharp
[Step(Order = 1, Entry = true)]
public Result<OrderCreated> Create(OrderDto dto) { ... }

[Step(Order = 2, AllowedAfter = new[] { nameof(Create) })]
public Result<OrderApproved> Approve(string approver) { ... }

[Step(Order = 2, AllowedAfter = new[] { nameof(Create) })]
public Result<OrderRejected> Reject(string reason) { ... }

[Step(Order = 3, AllowedAfter = new[] { nameof(Approve) })]
public Result<OrderPlaced> Place() { ... }

[Step(Order = 3, AllowedAfter = new[] { nameof(Reject) })]
public Result<OrderCancelled> Cancel() { ... }
```

The generator emits typestate as a DAG. After `Create`, `Approve` and `Reject` are both available. After `Approve` only `Place` is available. After `Reject` only `Cancel`. The compiler rejects every other combination.

This is *not* a state machine library — Crucible doesn't track aggregate state across executions. The branching enforces only the per-chain method validity. Cross-execution invariants (`if (Status != Draft) return error`) live in the aggregate methods themselves.

### Reconstruct from persistence

When an aggregate exists in the database, the chain resumes via `Orders.ReconstructAt[StepName](snapshot)`:

```csharp
// EF entity that implements the generator-emitted snapshot interface
public class OrderEntity : IOrderSnapshot
{
    public OrderId Id { get; set; }
    public string CustomerId { get; set; } = "";
    public Money Total { get; set; } = Money.Zero("USD");
    public OrderStatus Status { get; set; }
    public string? Carrier { get; set; }
    public long Version { get; set; }
    public IReadOnlyList<IOrderItemSnapshot> Items { get; set; } = Array.Empty<IOrderItemSnapshot>();
}

// Resume a chain
var entity = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == id);
var result = await Orders
    .ReconstructAtPlaceOrder(entity)
    .UpdateOrderInventory()
    .ExecuteAsync(sp, ct);
```

The required shape (private parameterless ctor + init properties on aggregates, entities, and value objects) aligns exactly with EF Core 8+ owned-type materialization. Configure once:

```csharp
modelBuilder.Entity<Order>().OwnsOne(o => o.Total);   // EF maps Money via private ctor + init properties
```

EF reflects on the private ctor and init setters; Crucible's `Create`/`Reconstruct` factories are independent paths. Two flows, zero conflict.

---

## Install

Three packages, typically referenced together:

```xml
<PackageReference Include="Crucible.Domain" Version="2.2.0" />
<PackageReference Include="Crucible.Chains" Version="2.2.0" />
<PackageReference Include="Crucible.Generators" Version="2.2.0" PrivateAssets="all" OutputItemType="Analyzer" />
```

Setup:

```csharp
services.AddCrucible(opts =>
{
    opts.AddBehavior<TracingBehavior>();         // optional cross-cutting decorators
    opts.EventDispatch(d =>
    {
        d.Mode = EventDispatchMode.Sequential;   // or Parallel
        d.OnHandlerError = HandlerErrorPolicy.LogAndContinue;
    });
});
services.AddOrderAggregate();                                              // generated registration
services.AddCrucibleEventHandler<OrderPlaced, NotifyWarehouseHandler>();
services.AddSingleton<IOrderRepository, EfOrderRepository>();
```

Target framework: `net10.0`. Generators target `netstandard2.0`. C# 14 features (extension members, partial properties) are required — `<LangVersion>preview</LangVersion>` until C# 14 ships as default.

---

## Diagnostics reference

27 active diagnostics enforce the structural contract:

| Code | Severity | Meaning |
|---|---|---|
| **CRC001** | Error | `[Aggregate]` has no `Entry = true` step |
| **CRC002** | Error | `[Aggregate]` has multiple `Entry = true` steps |
| **CRC003** | Error | Duplicate `[Step(Order = N)]` (linear mode) |
| **CRC004** | Error | Gap in `[Step(Order = N)]` sequence (linear mode) |
| **CRC005** | Error | `[Aggregate]` is not `partial` |
| **CRC006** | Error | `[Aggregate]` does not derive from `AggregateRoot<TId>` |
| **CRC007** | Error | `[Step]` returns something other than `Result<T>` or `Result` |
| **CRC008** | Error | `[Step]` is `async` or returns `Task` |
| **CRC010** | Error | Multiple handler implementations match the same step |
| **CRC011** | Error | `[Aggregate]` must not have public constructors |
| **CRC012** | Error | `AllowedAfter` references an unknown step |
| **CRC013** | Error | Step graph contains a cycle |
| **CRC014** | Error | Entry step must not declare `AllowedAfter` |
| **CRC015** | Error | Non-entry step has no resolvable predecessor (branching mode) |
| **CRC100** | Info | Step has no handler — runs as domain-only |
| **CRC200** | Warning | `[Pre<T>]` / `[Post<T>]` target does not implement the expected interface |
| **CRC300** | Error | `[Entity]` is not `partial` |
| **CRC301** | Error | `[Entity]` does not derive from `Entity<TId>` |
| **CRC302** | Error | `[Entity]` must have a parameterless constructor |
| **CRC303** | Error | No backing field found for entity collection |
| **CRC304** | Error | Multiple candidate backing fields for entity collection |
| **CRC305** | Error | `[Entity]` must not have public constructors |
| **CRC400** | Error | `[ValueObject]` must be `sealed partial record` |
| **CRC401** | Error | `[ValueObject]` must derive from `ValueObject` base record |
| **CRC402** | Error | `[ValueObject]` must not have public constructors |
| **CRC403** | Error | `[ValueObject]` must declare a private parameterless constructor |
| **CRC404** | Error | `[ValueObject]` properties must be `init`-only |

---

## Sample

A complete runnable ASP.NET sample demonstrating an Order aggregate with `OrderItem` entities, `Money` value object, and full chain execution lives at [`samples/Crucible.Sample.Orders/`](samples/Crucible.Sample.Orders/). The integration test suite at [`tests/Crucible.Sample.Orders.Tests/`](tests/Crucible.Sample.Orders.Tests/) contains 54 tests covering aggregate-level unit tests, chain-level integration tests, multi-handler event dispatch, value object validation, reconstruction with child entities, and HTTP endpoint behavior.

---

## What's in scope vs out of scope

In scope (v2.2.0):
- Aggregates, entities, value objects with structural enforcement
- Linear and branching chain execution with typestate
- Reconstruct/Snapshot for hydration from external persistence (EF Core etc.)
- IError contract with built-in error types
- Chain hooks: behaviors, pre/post processors, inline `Tap`/`OnError`/`ProducedEvents`/`DispatchEvents`
- Domain event dispatch with multi-handler support and error policies

Out of scope (use other libraries for these — Crucible composes inside them):
- Persistence (EF Core, Marten, Dapper — your choice)
- Queries / read models — Crucible is for commands
- CQRS pipeline / mediator dispatch (MediatR, Wolverine — Crucible lives inside the handler)
- Cross-aggregate orchestration / sagas (MassTransit, Wolverine sagas)
- HTTP / gRPC / queue consumers (ASP.NET, MassTransit)
- i18n / localized error messages (presentation layer maps `ErrorCode` → copy)

Roadmap candidates (post-v2.2):
- **`Crucible.OpenTelemetry`** — `IStepBehavior` for traces + metrics out of the box. Activity per chain step, standard tag set (`aggregate.name`, `step.name`, `step.kind`, `error.code`), metrics for chain duration, step success/failure rates, and event dispatch latency. Single optional dependency (`OpenTelemetry.Api`) so consumers who don't enable observability pay nothing.
- **Contoso reference app** — a substantial end-to-end demo (CRM- or e-commerce-flavored) that exercises every Crucible feature in a real-shaped codebase: multiple aggregates with cross-aggregate event flows, child entities with nested snapshots, value objects with EF Core mapping, branching workflows (approval, payment authorize/void/refund), Reconstruct from a real database, behaviors and pre/post processors, full HTTP layer with MediatR / Wolverine integration patterns. The current `Crucible.Sample.Orders` is a single-aggregate smoke test; the Contoso app is the "this is what a real project looks like" reference. Goal: a developer can clone, run, and read it for an afternoon and have a working mental model of how the pieces fit.
- `Crucible.Analyzers` — modeling-quality lints (god aggregates, anemic models, handler-to-handler calls, missing `.Catch` on chains with throwing handlers).
- More focused samples (approval workflow with branching as a standalone, multi-tenant patterns, event sourcing opt-in walkthrough).

---

## Versioning

Semantic versioning. Breaking changes bump the major version (`v2.0.0` introduced the `IError` contract; `v1.x` used `Error` directly). Changelog tracked via Git tags and Gitea releases.

---

## License

MIT.
