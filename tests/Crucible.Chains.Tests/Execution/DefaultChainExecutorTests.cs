using Crucible.Chains.Behaviors;
using Crucible.Chains.Execution;
using Crucible.Chains.Stages;
using Crucible.Chains.Steps;
using Crucible.Domain.Aggregates;
using Crucible.Domain.Errors;
using Crucible.Domain.Identifiers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crucible.Chains.Tests.Execution;

public sealed class DefaultChainExecutorTests
{
    private readonly record struct TId(Guid Value) : IAggregateId<TId>
    { public static TId New() => new(Guid.NewGuid()); public static TId From(Guid g) => new(g); }

    private sealed class A : AggregateRoot<TId>
    {
        public A() => Id = TId.New();
        public string? Tag { get; private set; }
        public void Mutate(string t) => Tag = t;
    }

    private sealed class FakeStep : IStep<A, TId>
    {
        private readonly Func<StepContext<A, TId>, StepOutcome> _impl;
        public FakeStep(string name, StepKind kind, Func<StepContext<A, TId>, StepOutcome> impl)
        { Name = name; Kind = kind; _impl = impl; }
        public StepKind Kind { get; }
        public string Name { get; }
        public Task<StepOutcome> InvokeAsync(StepContext<A, TId> ctx, CancellationToken ct)
            => Task.FromResult(_impl(ctx));
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new StepBehaviorPipeline(Array.Empty<IStepBehavior>()));
        services.AddSingleton<IChainExecutor, DefaultChainExecutor>();
        return services.BuildServiceProvider();
    }

    private static void AddStepToPlan(ChainPlan<A, TId> plan, IStep<A, TId> step)
    {
        plan.GetType()
            .GetMethod("Add", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(plan, new object[] { step });
    }

    [Fact]
    public async Task SuccessChain_ProducesSuccess()
    {
        var plan = new ChainPlan<A, TId>();
        AddStepToPlan(plan, new FakeStep("init", StepKind.AggregateMethod, ctx =>
        {
            ctx.Aggregate = new A();
            ctx.LastStepResult = "ok";
            return StepOutcome.Success("ok");
        }));

        var sp = BuildServices();
        var executor = sp.GetRequiredService<IChainExecutor>();
        var result = await executor.ExecuteAsync<A, TId, string>(plan, sp, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task FailureInStep_ProducesDomainFailure()
    {
        var plan = new ChainPlan<A, TId>();
        AddStepToPlan(plan, new FakeStep("init", StepKind.AggregateMethod, _ =>
            StepOutcome.Failure(new[] { (Error)new ValidationError("E", "m") })));

        var sp = BuildServices();
        var executor = sp.GetRequiredService<IChainExecutor>();
        var result = await executor.ExecuteAsync<A, TId, string>(plan, sp, CancellationToken.None);

        result.IsDomainFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExceptionInStep_ProducesExceptional()
    {
        var plan = new ChainPlan<A, TId>();
        AddStepToPlan(plan, new FakeStep("boom", StepKind.AggregateMethod, _ =>
            throw new InvalidOperationException("x")));

        var sp = BuildServices();
        var executor = sp.GetRequiredService<IChainExecutor>();
        var result = await executor.ExecuteAsync<A, TId, string>(plan, sp, CancellationToken.None);

        result.IsExceptional.Should().BeTrue();
    }
}
