using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class EntryEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        var entry = System.Linq.Enumerable.First(m.Steps, s => s.IsEntry);
        var aggFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

        cb.Line($"public static class {m.EntryClassName}");
        using (cb.Block())
        {
            // Original Create entry (the [Step(Entry = true)] method)
            EmitEntryStepMethod(cb, m, entry, aggFqn);

            // ReconstructAt[StepName] entries — one per step
            foreach (var step in m.Steps)
            {
                EmitReconstructEntry(cb, m, step, aggFqn);
            }
        }
    }

    private static void EmitEntryStepMethod(CodeBuilder cb, AggregateModel m, StepModel entry, string aggFqn)
    {
        var stateType = entry.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : entry.OutputTypeName!;
        var stage = $"global::Crucible.Chains.Stages.IChainStage<global::{aggFqn}, {m.IdTypeName}, {stateType}>";
        var paramsList = string.Join(", ", System.Linq.Enumerable.Select(entry.Parameters, p => $"{p.TypeName} {p.Name}"));
        var argsList = string.Join(", ", System.Linq.Enumerable.Select(entry.Parameters, p => p.Name));

        cb.Line($"public static {stage} {entry.MethodName}({paramsList})");
        using (cb.Block())
        {
            cb.Line($"return global::Crucible.Chains.Stages.ChainBuilder.Begin<global::{aggFqn}, {m.IdTypeName}, {stateType}>(new __Step_{entry.MethodName}({argsList}));");
        }
    }

    private static void EmitReconstructEntry(CodeBuilder cb, AggregateModel m, StepModel step, string aggFqn)
    {
        var stateType = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;
        var stage = $"global::Crucible.Chains.Stages.IChainStage<global::{aggFqn}, {m.IdTypeName}, {stateType}>";
        var snapshotIface = $"global::{(string.IsNullOrEmpty(m.Namespace) ? "" : m.Namespace + ".")}I{m.ClassName}Snapshot";

        cb.Line($"public static {stage} ReconstructAt{step.MethodName}({snapshotIface} snapshot)");
        using (cb.Block())
        {
            cb.Line($"return global::Crucible.Chains.Stages.ChainBuilder.Begin<global::{aggFqn}, {m.IdTypeName}, {stateType}>(new __Step_ReconstructAt{step.MethodName}(snapshot));");
        }
    }
}
