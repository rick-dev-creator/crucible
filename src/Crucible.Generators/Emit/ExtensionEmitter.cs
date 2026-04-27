using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class ExtensionEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        var aggFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

        cb.Line($"public static class {m.ClassName}ChainExtensions");
        using (cb.Block())
        {
            // For each step, emit an extension block on its stage type containing methods
            // of all OTHER steps whose AllowedAfter includes this step's name.
            foreach (var step in m.Steps)
            {
                var stateType = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;
                var stageType = $"global::Crucible.Chains.Stages.IChainStage<global::{aggFqn}, {m.IdTypeName}, {stateType}>";

                // Find all steps reachable from this step (i.e., steps whose AllowedAfter contains step.MethodName)
                var nextSteps = m.Steps
                    .Where(s => !s.IsEntry && s.AllowedAfter is { } aa && aa.Contains(step.MethodName))
                    .ToArray();

                cb.Line($"extension({stageType} stage)");
                using (cb.Block())
                {
                    foreach (var next in nextSteps)
                    {
                        var nextState = next.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : next.OutputTypeName!;
                        var nextStageType = $"global::Crucible.Chains.Stages.IChainStage<global::{aggFqn}, {m.IdTypeName}, {nextState}>";
                        var paramsList = string.Join(", ", System.Linq.Enumerable.Select(next.Parameters, p => $"{p.TypeName} {p.Name}"));
                        var argsList = string.Join(", ", System.Linq.Enumerable.Select(next.Parameters, p => p.Name));
                        cb.Line($"public {nextStageType} {next.MethodName}({paramsList})");
                        using (cb.Block())
                        {
                            cb.Line($"return global::Crucible.Chains.Stages.ChainBuilder.AppendStep<global::{aggFqn}, {m.IdTypeName}, {stateType}, {nextState}>(stage, new __Step_{next.MethodName}({argsList}));");
                        }
                    }
                }
            }
        }
    }
}
