using Crucible.Chains.Behaviors;
using Crucible.Chains.DependencyInjection;
using Crucible.Chains.Events;
using Crucible.Chains.Execution;
using Crucible.Chains.Steps;
using Crucible.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Crucible.Chains.Tests.DependencyInjection;

public sealed class AddCrucibleTests
{
    private sealed class NoopBehavior : IStepBehavior
    {
        public Task<StepOutcome> InvokeAsync(StepDescriptor s, Func<Task<StepOutcome>> next, IServiceProvider sp, CancellationToken ct) => next();
    }

    [Fact]
    public void RegistersExecutorPipelineDispatcherOptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        services.AddCrucible(opts => opts.AddBehavior<NoopBehavior>());

        var sp = services.BuildServiceProvider();
        sp.GetService<IChainExecutor>().Should().NotBeNull();
        sp.GetService<IDomainEventDispatcher>().Should().NotBeNull();
        sp.GetService<StepBehaviorPipeline>().Should().NotBeNull();
        sp.GetService<EventDispatchOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddCrucibleEventHandler_RegistersAsScoped()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddCrucible();
        services.AddCrucibleEventHandler<TestEvent, TestHandler>();

        var sp = services.BuildServiceProvider();
        var handlers = sp.GetServices<IDomainEventHandler<TestEvent>>().ToArray();
        handlers.Should().ContainSingle();
    }

    private sealed record TestEvent(string Tag) : DomainEvent;
    private sealed class TestHandler : IDomainEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken ct) => Task.CompletedTask;
    }
}
