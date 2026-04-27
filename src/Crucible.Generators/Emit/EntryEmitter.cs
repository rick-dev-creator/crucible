using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class EntryEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        var entry = System.Linq.Enumerable.First(m.Steps, s => s.IsEntry);
        var firstPhase = $"I{m.ClassName}After{entry.MethodName}";
        var stateType = entry.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : entry.OutputTypeName!;
        var paramsList = string.Join(", ", System.Linq.Enumerable.Select(entry.Parameters, p => $"{p.TypeName} {p.Name}"));
        var argsList = string.Join(", ", System.Linq.Enumerable.Select(entry.Parameters, p => p.Name));

        cb.Line($"public static class {m.EntryClassName}");
        using (cb.Block())
        {
            cb.Line($"public static {firstPhase} {entry.MethodName}({paramsList})");
            using (cb.Block())
            {
                cb.Line($"return ({firstPhase})global::Crucible.Chains.Stages.ChainBuilder.Begin<global::{Fqn(m)}, {m.IdTypeName}, {stateType}>(new __Step_{entry.MethodName}({argsList}));");
            }
        }
    }

    private static string Fqn(AggregateModel m) => string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";
}
