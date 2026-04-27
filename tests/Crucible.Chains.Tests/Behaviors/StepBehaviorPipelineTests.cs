using Crucible.Chains.Behaviors;
using Crucible.Chains.Steps;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Crucible.Chains.Tests.Behaviors;

public sealed class StepBehaviorPipelineTests
{
    private sealed class RecordingBehavior : IStepBehavior
    {
        private readonly string _tag;
        private readonly List<string> _log;
        public RecordingBehavior(string tag, List<string> log) { _tag = tag; _log = log; }

        public async Task<StepOutcome> InvokeAsync(StepDescriptor step, Func<Task<StepOutcome>> next, IServiceProvider services, CancellationToken ct)
        {
            _log.Add($"{_tag}-before");
            var outcome = await next().ConfigureAwait(false);
            _log.Add($"{_tag}-after");
            return outcome;
        }
    }

    [Fact]
    public async Task Behaviors_ComposeInRegistrationOrder()
    {
        var log = new List<string>();
        var pipeline = new StepBehaviorPipeline(new IStepBehavior[]
        {
            new RecordingBehavior("a", log),
            new RecordingBehavior("b", log),
        });
        var sp = new ServiceCollection().BuildServiceProvider();
        var desc = new StepDescriptor("Order", "Create", StepKind.AggregateMethod, null, null);
        await pipeline.RunAsync(desc, () => Task.FromResult(StepOutcome.Success()), sp, CancellationToken.None);
        log.Should().Equal("a-before", "b-before", "b-after", "a-after");
    }

    [Fact]
    public async Task EmptyPipeline_InvokesInnerOnce()
    {
        var pipeline = new StepBehaviorPipeline(Array.Empty<IStepBehavior>());
        var calls = 0;
        var sp = new ServiceCollection().BuildServiceProvider();
        var desc = new StepDescriptor("X", "Y", StepKind.AggregateMethod, null, null);
        await pipeline.RunAsync(desc, () => { calls++; return Task.FromResult(StepOutcome.Success()); }, sp, CancellationToken.None);
        calls.Should().Be(1);
    }
}
