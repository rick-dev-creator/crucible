using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class PhaseEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        for (int i = 0; i < m.Steps.Count; i++)
        {
            var step = m.Steps[i];
            var phase = $"I{m.ClassName}After{step.MethodName}";
            var stateType = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;
            var aggFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

            cb.Line($"public interface {phase} : global::Crucible.Chains.Stages.IChainStage<global::{aggFqn}, {m.IdTypeName}, {stateType}> {{ }}");
        }
    }
}
