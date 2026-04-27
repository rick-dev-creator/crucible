using Crucible.Chains.Steps;

namespace Crucible.Chains.Behaviors;

public interface IStepBehavior
{
    Task<StepOutcome> InvokeAsync(
        StepDescriptor step,
        Func<Task<StepOutcome>> next,
        IServiceProvider services,
        CancellationToken ct);
}
