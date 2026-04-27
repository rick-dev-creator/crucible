# Crucible

> Opinionated DDD library for **.NET 10 / C# 14**. Compile-time enforcement of aggregates, entities, value objects, and chain composition — so LLMs and junior developers cannot fragment the domain.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-2.2.0-blue.svg)](https://github.com/rick-dev-creator/crucible/releases)
[![Tests](https://img.shields.io/badge/tests-144%20passing-green.svg)]()

---

## Table of contents

1. [What is Crucible?](#what-is-crucible)
2. [The problem it solves](#the-problem-it-solves)
3. [How it solves it](#how-it-solves-it)
4. [Is this for you?](#is-this-for-you)
5. [Quickstart](#quickstart)
6. [Where Crucible fits in your stack](#where-crucible-fits-in-your-stack)
7. [Concepts](#concepts)
8. [Diagnostics reference](#diagnostics-reference)
9. [Motivation](#motivation)
10. [Roadmap](#roadmap)
11. [Versioning](#versioning)
12. [License](#license)

---

## What is Crucible?

Crucible is a **library + Roslyn source generator** that encodes DDD discipline as compile-time constraints. You declare your domain types with a few attributes (`[Aggregate]`, `[Entity]`, `[ValueObject]`, `[Step]`), and the generator produces a typestate-enforced fluent chain plus 27 diagnostics that block common DDD mistakes at build time.

The thesis is simple: **anything you "ask" a developer (human or AI) to do, they will eventually skip. Anything that doesn't compile cannot ship.** Crucible converts DDD principles from documentation into compile errors.

> **Status: v2.2.0 — production-ready core.** Aggregates, entities, value objects, chain runtime, branching typestate, EF Core-friendly Reconstruct/Snapshot, IError contract. 144 tests passing.

---

## The problem it solves

### Symptoms in real codebases

Building a non-trivial business system with LLMs or rotating juniors tends to produce the same structural rot:

| Symptom | Why it ships |
|---|---|
| 800-line `OrderService` doing everything | No structural pressure to split |
| Same validation rule duplicated in 5 places | Each layer "validates just to be safe" |
| Domain events raised from controllers | No clear ownership of state changes |
| Handlers calling handlers calling handlers | No boundary to stop the fan-out |
| Silent `null` fallbacks | Caller doesn't notice; tests don't either |
| User-facing copy in domain returns | Domain leaks into UI concerns |
| Public ctors letting anyone bypass validation | Convention broken once, never fixed |
| Workflow rules as scattered `if` checks | Easy to forget in a new method |

### Why code review doesn't fix this

Each of the above is *fixable* in code review. None is fixable in code review **every time** on a project that runs for months, with team turnover, and where 60% of code is AI-generated. The drift is structural — the defense has to be structural too.

---

## How it solves it

### At compile time

The generator produces typestate from your domain declaration. Things that compile today and shouldn't:

```csharp
new Order();                              // ❌ CRC011 — Aggregate ctor must be private
new Money(100m, "USD");                   // ❌ CRC402 — VO ctor must be private; use Create
Orders.PlaceOrder(...);                   // ❌ Compile error — typestate requires Create first
Orders.Create(dto).UpdateInventory();     // ❌ Compile error — must go through PlaceOrder first
return new Error("Customer not found");   // ❌ Won't compile — IError requires ErrorCode + ErrorDescription
```

### At runtime

Each chain step runs in a fixed order: behaviors → pre-processors → aggregate method → handler → post-processors → event accumulation. The handler runs **only if the aggregate method returned `Result.Success`** — validation cannot be bypassed by infrastructure code.

### At the boundary

Each Crucible layer owns exactly one failure mode:

| Layer | Owns | Surface |
|---|---|---|
| Aggregate methods | Business rule failures | `Result<T>.Failure(IError[])` |
| Step handlers | Infrastructure failures | `.Catch()` translates exceptions |
| Domain event handlers | Post-commit side effects | Configurable error policy |
| Source-generated typestate | Composition failures | Compile errors |

### Through 27 diagnostics

`CRC001` through `CRC404` enforce the contract. A team that adds a public ctor to an aggregate, returns a free-form `Message`, or composes chain methods in an invalid order doesn't get a code review comment. They get a build failure.

[See the full diagnostics reference →](#diagnostics-reference)

---

## Is this for you?

### ✅ Good fit if...

- You're building a non-trivial business system (CRM, billing, line-of-business app)
- You have **multi-step domain workflows** with real invariants
- Your team has **turnover** or you collaborate with **AI code generators**
- You read DDD orthodox-leaning (Vaughn Vernon flavor) and want it enforced
- You prefer **Result-based error handling** over exceptions for business rules
- You can adopt **opinionated conventions** (private ctors, factory methods, no Message strings in domain)

### ❌ Not a fit if...

- Your team prefers **anemic domain models** with services orchestrating mutations
- You prefer **exceptions for domain rules** — Crucible's `Result<T>` will feel pedantic
- You need **public constructors and factory methods** on aggregates — Crucible blocks them
- Your aggregates are **mostly CRUD** with no multi-step workflows — overkill
- You need **event sourcing as first-class** — v2.2 is state-based; ES is on the roadmap, not shipped
- You need **cross-aggregate orchestration / sagas with compensation** — use MassTransit or Wolverine

> **Litmus test:** read the [diagnostics list](#diagnostics-reference). If you find yourself wanting to suppress half of them, this isn't the library for you. If you read them and think "yes, exactly, my team should hit all of these" — you're the audience.

---

## Quickstart

### 1. Install the packages

```xml
<PackageReference Include="Crucible.Domain" Version="2.2.0" />
<PackageReference Include="Crucible.Chains" Version="2.2.0" />
<PackageReference Include="Crucible.Generators" Version="2.2.0" PrivateAssets="all" OutputItemType="Analyzer" />
```

Target framework: `net10.0`. C# 14 features (extension members, partial properties) require `<LangVersion>preview</LangVersion>` until C# 14 ships as default.

### 2. Define a value object

Construction goes through the generator-emitted `Create` factory. No invalid `Money` ever exists in memory.

```csharp
[ValueObject]
public sealed partial record Money : ValueObject
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "";

    private Money() { }   // CRC403 — required for hydration + EF Core

    private static partial Result __ValidateConstruction(decimal amount, string currency)
    {
        var errors = new List<IError>();
        if (amount < 0)
            errors.Add(new ValidationError("MONEY_NEGATIVE_AMOUNT", "Amount must be non-negative"));
        if (string.IsNullOrWhiteSpace(currency))
            errors.Add(new ValidationError("MONEY_CURRENCY_REQUIRED", "Currency is required"));
        return errors.Count > 0 ? Result.Failure(errors) : Result.Success();
    }
}
```

### 3. Define an aggregate

Each `[Step]` method is a node in the typestate chain. The generator emits the static entry class (`Orders` for `Order`).

```csharp
[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private Order() { }   // CRC011 — must be private

    public string CustomerId { get; private set; } = "";
    public Money Total { get; private set; } = Money.Zero("USD");
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;

    [Step(Order = 1, Entry = true)]
    public Result<OrderCreated> Create(OrderDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
            return new ValidationError("ORDER_CUSTOMER_REQUIRED", "CustomerId is required");

        var totalResult = Money.Create(dto.Amount, dto.Currency);
        if (totalResult.IsFailure) return Result<OrderCreated>.Failure(totalResult.Errors);

        Id = OrderId.New();
        CustomerId = dto.CustomerId;
        Total = totalResult.Value;
        var evt = new OrderCreated(Id, CustomerId, Total);
        Raise(evt);
        return evt;
    }

    [Step(Order = 2)]
    public Result<OrderPlaced> PlaceOrder(ShippingOptions s) { /* ... */ }
}
```

### 4. Wire DI

```csharp
services.AddCrucible();
services.AddOrderAggregate();   // generated registration
services.AddCrucibleEventHandler<OrderPlaced, NotifyWarehouseHandler>();
services.AddSingleton<IOrderRepository, EfOrderRepository>();
```

### 5. Use the chain

```csharp
app.MapPost("/orders", async (OrderDto dto, IServiceProvider sp, CancellationToken ct) =>
{
    return await Orders
        .Create(dto)
        .PlaceOrder(new ShippingOptions("UPS", 2))
        .DispatchEvents()
        .ExecuteAsync(sp, ct)
        .Catch(ex => new IError[] { new InfrastructureError("ORDER_PIPELINE", ex.Message) })
        .Match(
            success => Results.Ok(success),
            errors  => Results.BadRequest(errors));
});
```

> **The chain is deferred.** Methods before `ExecuteAsync` (including `DispatchEvents`) only append steps to the plan. `ExecuteAsync` runs the assembled plan in order. `DispatchEvents` placed before `ExecuteAsync` is the canonical position — events fire as the last step, only after every prior step succeeded. Mid-chain placement is also valid. Same model as LINQ: `.Where().Select()` is just a plan; `.ToList()` runs it.

### Compile-time guarantees in this example

| Attempted misuse | What stops it |
|---|---|
| `new Order()` | CRC011 — `[Aggregate]` ctor is private |
| `new Money(100m, "USD")` | CRC402 — `[ValueObject]` ctor is private |
| `Orders.PlaceOrder(...)` directly | Compile error — `PlaceOrder` not on starting type |
| Skipping a step in the chain | Compile error — typestate restricts available methods |
| Returning `string Message` from domain | The library forces `IError` |
| `throw new BusinessRuleException(...)` in domain | No such type — business rules use `Result.Failure` |

> 📂 **Full runnable sample**: [`samples/Crucible.Sample.Orders/`](samples/Crucible.Sample.Orders/) — ASP.NET app with `OrderItem` child entities, integration tests, and 54 tests covering aggregate-level, chain-level, and HTTP-level scenarios.

---

## Where Crucible fits in your stack

Crucible is a **domain engine**. It does not replace your CQRS / mediator / HTTP layer. It lives **inside** your command handler:

```
HTTP / gRPC / queue consumer
       │
       ▼
Application layer (MediatR, Wolverine, raw service classes — your choice)
       │
       ▼
[CommandHandler.Handle calls Crucible chain]    ◄── Crucible operates here
       │
       ▼
Persistence (EF Core, Marten, Dapper — your choice)
```

### Example: inside a MediatR handler

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

### Queries are intentionally out of scope

For `GetOrderById`, `ListOrdersByCustomer`, etc., Crucible does nothing — queries don't go through aggregates. Use whatever you prefer (Dapper, EF Core query, MediatR query handler). CQRS separation is the consumer's concern.

---

## Concepts

### `[Aggregate]`

The transactional boundary of your domain.

**Requirements** (enforced via diagnostics):
- `partial` class inheriting `AggregateRoot<TId>` (CRC005, CRC006)
- `private` parameterless constructor (CRC011)
- One or more `[Step]` methods, exactly one with `Entry = true` (CRC001, CRC002)

**What the generator emits:**
- A static entry class (`Orders` for `Order`) with the entry method
- Per-step extension methods that gate composition through typestate
- Reconstruct entries (`Orders.ReconstructAtPlaceOrder(snapshot)`, etc.) — one per step
- `IOrderSnapshot` interface and hydration helper for persistence

```csharp
[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private Order() { }
    [Step(Order = 1, Entry = true)] public Result<OrderCreated> Create(OrderDto dto) { ... }
    [Step(Order = 2)] public Result<OrderPlaced> Place(ShippingOptions s) { ... }
}
```

---

### `[Entity]`

A child object inside an aggregate. Has identity (Id-based equality) but no chain entry of its own.

**Requirements:**
- `partial` class inheriting `Entity<TId>` (CRC300, CRC301)
- `private` parameterless constructor (CRC305)

**What the generator emits:**
- `IEntitySnapshot` interface
- `__HydrateFromSnapshot` partial method
- `static RehydrateFrom` factory

If an aggregate holds an `IReadOnlyList<TEntity>` property, the children are automatically referenced in the aggregate's snapshot interface and rehydrated by its hydration helper.

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

---

### `[ValueObject]`

Identity-less, immutable, structurally compared. Construction is forced through a `Create` factory.

**Requirements:**
- `sealed partial record` deriving from `ValueObject` (CRC400, CRC401)
- `private` parameterless constructor (CRC403)
- All properties `init`-only (CRC404)
- No public constructors (CRC402)

**What the generator emits:**
- `public static Result<TVO> Create(...)` factory
- A declaration of `private static partial Result __ValidateConstruction(...)` that the developer must implement

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

---

### `Result<T>` and `IError`

Domain methods return `Result<T>`. Errors implement `IError`:

```csharp
public interface IError
{
    string ErrorCode { get; }            // stable, machine-readable identifier
    string ErrorDescription { get; }     // for internal logging only — NOT user-facing
    ErrorKind Kind { get; }
}
```

**Built-in implementations:** `ValidationError`, `BusinessRuleError`, `ConflictError`, `NotFoundError`, `InfrastructureError`.

**Custom errors:** implement `IError` directly (e.g., for tenant-scoped errors with extra fields), or extend the abstract `Error` record for the common case.

> **Why no `Message`?** The library forbids free-form string messages from the domain. `ErrorDescription` is named explicitly for **internal logging only**. Localized user-facing copy lives in the presentation layer, mapping `ErrorCode` → translated string. The domain identifies; presentation localizes.

`Result<T>` and `ChainResult<T>` are exhaustive: `Match(success, failure)` covers both branches. There is no path that silently ignores errors.

---

### Chain runtime

Each `[Step]` method becomes a node in a fluent chain. The chain is **deferred**: methods append to a plan, `ExecuteAsync` runs it.

**Per aggregate-method step, the executor runs (in order):**
1. `IStepBehavior` decorators (cross-cutting: tracing, logging, metrics)
2. `[Pre<TPre>]` processors (validation, authorization)
3. The aggregate method (synchronous, returns `Result<TOutput>`)
4. The `IStepHandler<...>` (async, returns `Result`; **only if** the aggregate method succeeded)
5. `[Post<TPost>]` processors (telemetry, fire-and-forget side effects)
6. Pending events drained into the chain's accumulated event log

**Inline hooks (`Tap`, `OnError`, `ProducedEvents`, `DispatchEvents`)** are also chain steps — placement matters:

```csharp
// (a) Dispatch at the end — events fire only after every prior step succeeded:
.Create(dto).PlaceOrder(s).UpdateOrderInventory().DispatchEvents().ExecuteAsync(sp, ct);

// (b) Dispatch mid-chain — events from Create fire before PlaceOrder runs:
.Create(dto).DispatchEvents().PlaceOrder(s).ExecuteAsync(sp, ct);

// (c) ProducedEvents as inspection without drain (e.g., for logging):
.Create(dto)
.ProducedEvents(events => log.LogInfo("after Create: {N} events", events.Count), drain: false)
.PlaceOrder(s).DispatchEvents().ExecuteAsync(sp, ct);
```

After execution, `ChainResult<T>` exposes three states (`Success`, `DomainFailure`, `Exceptional`) and the cumulative event log. `Match` is exhaustive over success/failure; `Catch` translates exceptions before `Match` sees the result.

---

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

**The result:**
- After `Create`: both `Approve` and `Reject` are available
- After `Approve`: only `Place` is available
- After `Reject`: only `Cancel` is available
- Every other combination fails to compile

> **Branching is not a state machine library.** Crucible does not track aggregate state across executions. Cross-execution invariants (`if (Status != Draft) return error`) live in the aggregate methods — that's runtime enforcement, complementary to the chain typestate enforcement. The two layers compose: typestate prevents typos, runtime invariants catch mid-execution logic errors.

---

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

// Resume a chain at the correct phase
var entity = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == id);
var result = await Orders
    .ReconstructAtPlaceOrder(entity)
    .UpdateOrderInventory()
    .ExecuteAsync(sp, ct);
```

**EF Core compatibility is built in.** The required shape — `private` parameterless ctor + `init` properties on aggregates, entities, and value objects — is exactly what EF Core 8+ uses for owned-type materialization:

```csharp
modelBuilder.Entity<Order>().OwnsOne(o => o.Total);   // EF maps Money via private ctor + init properties
```

EF reflects on the private ctor and init setters; Crucible's `Create`/`Reconstruct` factories are independent paths. **Two flows, zero conflict.**

---

## Diagnostics reference

27 active diagnostics. Most are `Error` severity — they fail the build.

### `[Aggregate]` (CRC001–CRC015)

| Code | Severity | Meaning |
|---|---|---|
| **CRC001** | Error | Aggregate has no `Entry = true` step |
| **CRC002** | Error | Aggregate has multiple `Entry = true` steps |
| **CRC003** | Error | Duplicate `[Step(Order = N)]` (linear mode only) |
| **CRC004** | Error | Gap in `[Step(Order = N)]` sequence (linear mode only) |
| **CRC005** | Error | Aggregate is not `partial` |
| **CRC006** | Error | Aggregate does not derive from `AggregateRoot<TId>` |
| **CRC007** | Error | `[Step]` returns something other than `Result<T>` or `Result` |
| **CRC008** | Error | `[Step]` is `async` or returns `Task` |
| **CRC010** | Error | Multiple handler implementations match the same step |
| **CRC011** | Error | Aggregate must not have public constructors |
| **CRC012** | Error | `AllowedAfter` references an unknown step |
| **CRC013** | Error | Step graph contains a cycle |
| **CRC014** | Error | Entry step must not declare `AllowedAfter` |
| **CRC015** | Error | Non-entry step has no resolvable predecessor (branching mode) |

### Steps and processors (CRC100, CRC200)

| Code | Severity | Meaning |
|---|---|---|
| **CRC100** | Info | Step has no handler — runs as domain-only |
| **CRC200** | Warning | `[Pre<T>]` / `[Post<T>]` target does not implement the expected interface |

### `[Entity]` (CRC300–CRC305)

| Code | Severity | Meaning |
|---|---|---|
| **CRC300** | Error | Entity is not `partial` |
| **CRC301** | Error | Entity does not derive from `Entity<TId>` |
| **CRC302** | Error | Entity must have a parameterless constructor |
| **CRC303** | Error | No backing field found for entity collection |
| **CRC304** | Error | Multiple candidate backing fields for entity collection |
| **CRC305** | Error | Entity must not have public constructors |

### `[ValueObject]` (CRC400–CRC404)

| Code | Severity | Meaning |
|---|---|---|
| **CRC400** | Error | Value object must be `sealed partial record` |
| **CRC401** | Error | Value object must derive from `ValueObject` base record |
| **CRC402** | Error | Value object must not have public constructors |
| **CRC403** | Error | Value object must declare a private parameterless constructor |
| **CRC404** | Error | Value object properties must be `init`-only |

---

## Motivation

### Origin

This is built on close to **two decades of C# work** and a long list of enterprise applications — CRMs, billing systems, line-of-business apps. The technique itself isn't new for me; I've used variations on internal projects for years. What's new is materializing it as an open-source library so other teams can adopt the parts they want.

### The LLM amplifier

I have used LLMs for code almost daily for over a year — partly to learn, partly to **test the productivity claims** executives keep repeating. The honest answer is mixed. LLMs do produce. They help. With strong foundations and concrete knowledge of what you're building, they make a senior measurably more productive.

Without those foundations, the picture flips. **LLMs amplify the developer using them, in both directions.** A senior who knows what to ask for and reviews carefully gets faster and more consistent output. A junior who treats LLM output as authoritative ships subtly broken code at five times the speed.

In practice on enterprise codebases, the negative amplification has been **more visible than the positive**. LLMs reliably:

- introduce patterns that weren't asked for and don't fit the project,
- ignore explicit rules in `CLAUDE.md` / `.cursorrules` / system prompts after a handful of turns,
- invent abstractions to "improve" code that didn't need them,
- silently change conventions across files in ways code review struggles to catch,
- and output with a confidence level that doesn't match their actual correctness.

The end result is an agent that behaves like a junior with very fast hands and **dangerous self-assurance** — the kind an inexperienced developer reads as authority and accepts blindly. A team with strong reviewers can absorb that drift. A team without strong reviewers ships it.

The technical debt then compounds fast. Without solid foundations, an LLM-heavy codebase will end up **worse than the microservices wave** of the late 2010s — when teams were sold a design as universal best practice, applied it to problems it didn't fit, and spent years unwinding the result. The pattern is the same: an attractive-sounding answer (microservices then, "LLM productivity" now) applied without the judgment to know when it fits, with the bill arriving years later in maintenance and rewrites.

That is the second motivation for this framework. Documentation, system prompts, code review, and lint rules all assume someone reads and follows them. **None of those assumptions hold reliably with LLMs in the loop.** What does hold is the compiler. Code that doesn't compile cannot be merged regardless of who (or what) wrote it. Crucible's bet is that the cheapest place to enforce DDD discipline in an LLM-collaborative team is the type system, not the review process.

### Humility

That experience earns credibility for the patterns, but **it does not make this the right answer** — only one defensible answer among several.

DDD has been written about for 20+ years by smarter people than me. Teams have legitimately read it differently and built successful systems on opposing premises. Crucible enforces one specific reading — the one that matched my projects. If your reading differs, your library differs. **That's healthy.**

A framework that tries to please everyone enforces nothing. Crucible doesn't try.

---

## Roadmap

Candidates for post-v2.2:

### `Crucible.OpenTelemetry`

`IStepBehavior` for traces and metrics out of the box. `Activity` per chain step with a standard tag set (`aggregate.name`, `step.name`, `step.kind`, `error.code`); metrics for chain duration, step success/failure rates, and event dispatch latency. Single optional dependency (`OpenTelemetry.Api`) — consumers who don't enable observability pay nothing.

### Contoso reference app

A substantial end-to-end demo (CRM- or e-commerce-flavored) exercising every Crucible feature in a real-shaped codebase:

- Multiple aggregates with cross-aggregate event flows
- Child entities with nested snapshots
- Value objects with EF Core mapping
- Branching workflows (approval, payment authorize/void/refund)
- Reconstruct from a real database
- Behaviors and pre/post processors in production-like use
- Full HTTP layer with MediatR / Wolverine integration patterns

> The current `Crucible.Sample.Orders` is a single-aggregate smoke test. The Contoso app is the **"this is what a real project looks like"** reference. Goal: clone, run, read for an afternoon → working mental model.

### `Crucible.Analyzers`

Modeling-quality lints (god aggregates, anemic models, handler-to-handler calls, missing `.Catch` on chains with throwing handlers).

### Focused samples

Approval workflow with branching (standalone), multi-tenant patterns, event sourcing opt-in walkthrough.

---

## Versioning

Semantic versioning. Breaking changes bump the major version (e.g., `v2.0.0` introduced the `IError` contract; `v1.x` used `Error` directly).

Changelog tracked via [Git tags](https://github.com/rick-dev-creator/crucible/tags) and [GitHub releases](https://github.com/rick-dev-creator/crucible/releases).

NuGet packages are attached to GitHub releases starting from `v2.2.0`.

---

## License

[MIT](LICENSE).
