using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class ExtensionEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        cb.Line($"public static class {m.ClassName}ChainExtensions");
        using (cb.Block())
        {
            for (int i = 0; i < m.Steps.Count; i++)
            {
                var step = m.Steps[i];
                var phase = $"I{m.ClassName}After{step.MethodName}";
                var stateType = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;

                cb.Line($"extension({phase} stage)");
                using (cb.Block())
                {
                    if (i + 1 < m.Steps.Count)
                    {
                        var next = m.Steps[i + 1];
                        var nextPhase = $"I{m.ClassName}After{next.MethodName}";
                        var nextState = next.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : next.OutputTypeName!;
                        var paramsList = string.Join(", ", System.Linq.Enumerable.Select(next.Parameters, p => $"{p.TypeName} {p.Name}"));
                        var argsList = string.Join(", ", System.Linq.Enumerable.Select(next.Parameters, p => p.Name));
                        cb.Line($"public {nextPhase} {next.MethodName}({paramsList})");
                        using (cb.Block())
                        {
                            cb.Line($"return ({nextPhase})global::Crucible.Chains.Stages.ChainBuilder.AppendStep<global::{Fqn(m)}, {m.IdTypeName}, {stateType}, {nextState}>(stage, new __Step_{next.MethodName}({argsList}));");
                        }
                    }

                    EmitUniversalMembers(cb, phase, stateType);
                }
            }
        }
    }

    private static void EmitUniversalMembers(CodeBuilder cb, string phase, string stateType)
    {
        cb.Line($"public {phase} Tap(global::System.Action<{stateType}> action)");
        using (cb.Block())
        {
            cb.Line($"return ({phase})stage.Tap(action);");
        }
        cb.Line($"public {phase} Tap(global::System.Func<{stateType}, global::System.IServiceProvider, global::System.Threading.CancellationToken, global::System.Threading.Tasks.Task> action)");
        using (cb.Block())
        {
            cb.Line($"return ({phase})stage.Tap(action);");
        }
        cb.Line($"public {phase} OnError(global::System.Action<global::System.Collections.Generic.IReadOnlyList<global::Crucible.Domain.Errors.Error>> action)");
        using (cb.Block())
        {
            cb.Line($"return ({phase})stage.OnError(action);");
        }
        cb.Line($"public {phase} ProducedEvents(global::System.Action<global::System.Collections.Generic.IReadOnlyList<global::Crucible.Domain.Events.IDomainEvent>> callback, bool drain = true)");
        using (cb.Block())
        {
            cb.Line($"return ({phase})stage.ProducedEvents(callback, drain);");
        }
        cb.Line($"public {phase} DispatchEvents()");
        using (cb.Block())
        {
            cb.Line($"return ({phase})stage.DispatchEvents();");
        }
    }

    private static string Fqn(AggregateModel m) => string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";
}
