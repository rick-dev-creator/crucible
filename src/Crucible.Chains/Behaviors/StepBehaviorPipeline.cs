using Crucible.Chains.Steps;

namespace Crucible.Chains.Behaviors;

public sealed class StepBehaviorPipeline
{
    private readonly IReadOnlyList<IStepBehavior> _behaviors;

    public StepBehaviorPipeline(IEnumerable<IStepBehavior> behaviors)
    {
        _behaviors = behaviors.ToArray();
    }

    public Task<StepOutcome> RunAsync(
        StepDescriptor step,
        Func<Task<StepOutcome>> inner,
        IServiceProvider services,
        CancellationToken ct)
    {
        Func<Task<StepOutcome>> chain = inner;
        for (var i = _behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = _behaviors[i];
            var next = chain;
            chain = () => behavior.InvokeAsync(step, next, services, ct);
        }
        return chain();
    }
}
