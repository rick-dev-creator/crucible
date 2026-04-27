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
            for (int i = 0; i < m.Steps.Count; i++)
            {
                var step = m.Steps[i];
                var stateType = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;
                var stageType = $"global::Crucible.Chains.Stages.IChainStage<global::{aggFqn}, {m.IdTypeName}, {stateType}>";

                cb.Line($"extension({stageType} stage)");
                using (cb.Block())
                {
                    if (i + 1 < m.Steps.Count)
                    {
                        var next = m.Steps[i + 1];
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
