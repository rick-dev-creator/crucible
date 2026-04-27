using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class StepEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        var aggFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

        foreach (var step in m.Steps)
        {
            var stateType = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;
            var ctorParams = string.Join(", ", System.Linq.Enumerable.Select(step.Parameters, p => $"{p.TypeName} {p.Name}"));
            var ctorAssigns = string.Join(" ", System.Linq.Enumerable.Select(step.Parameters, p => $"this._{p.Name} = {p.Name};"));
            var fields = string.Concat(System.Linq.Enumerable.Select(step.Parameters, p => $"private readonly {p.TypeName} _{p.Name}; "));

            // Determine the input type for the handler resolution.
            // If method has 1 param: use that param's type. If 0 params: use Unit.
            // Multiple params is not supported in v1.
            string inputType = step.Parameters.Count == 1
                ? step.Parameters[0].TypeName
                : "global::Crucible.Chains.Steps.Unit";
            string inputArg = step.Parameters.Count == 1
                ? "this._" + step.Parameters[0].Name
                : "global::Crucible.Chains.Steps.Unit.Value";

            // Argument list to pass to the aggregate method invocation.
            var methodArgList = string.Join(", ", System.Linq.Enumerable.Select(step.Parameters, p => $"this._{p.Name}"));

            cb.Line($"internal sealed class __Step_{step.MethodName} : global::Crucible.Chains.Steps.IStep<global::{aggFqn}, {m.IdTypeName}>");
            using (cb.Block())
            {
                if (!string.IsNullOrEmpty(fields)) cb.Line(fields);
                cb.Line($"public __Step_{step.MethodName}({ctorParams}) {{ {ctorAssigns} }}");
                cb.Line($"public global::Crucible.Chains.Steps.StepKind Kind => global::Crucible.Chains.Steps.StepKind.AggregateMethod;");
                cb.Line($"public string Name => \"{m.ClassName}.{step.MethodName}\";");
                cb.Line($"public async global::System.Threading.Tasks.Task<global::Crucible.Chains.Steps.StepOutcome> InvokeAsync(global::Crucible.Chains.Steps.StepContext<global::{aggFqn}, {m.IdTypeName}> ctx, global::System.Threading.CancellationToken ct)");
                using (cb.Block())
                {
                    if (step.IsEntry)
                    {
                        cb.Line($"ctx.Aggregate = new global::{aggFqn}();");
                    }
                    cb.Line($"var aggregate = ctx.Aggregate ?? throw new global::System.InvalidOperationException(\"Aggregate is null\");");

                    // Pre processors
                    for (int preIdx = 0; preIdx < step.PreProcessorTypes.Count; preIdx++)
                    {
                        cb.Line($"var pre_{preIdx} = (global::Crucible.Chains.Processors.IPreProcessor<global::{aggFqn}, {m.IdTypeName}, {inputType}>?)ctx.Services.GetService(typeof(global::Crucible.Chains.Processors.IPreProcessor<global::{aggFqn}, {m.IdTypeName}, {inputType}>));");
                        cb.Line($"if (pre_{preIdx} is not null)");
                        using (cb.Block())
                        {
                            cb.Line($"var preCtx_{preIdx} = new global::Crucible.Chains.Processors.PreContext<global::{aggFqn}, {m.IdTypeName}, {inputType}>(aggregate, {inputArg}, ctx.Services);");
                            cb.Line($"var preResult_{preIdx} = await pre_{preIdx}.InvokeAsync(preCtx_{preIdx}, ct).ConfigureAwait(false);");
                            cb.Line($"if (preResult_{preIdx}.IsFailure) return global::Crucible.Chains.Steps.StepOutcome.Failure(preResult_{preIdx}.Errors);");
                        }
                    }

                    // Aggregate method invocation
                    cb.Line($"var domainResult = aggregate.{step.MethodName}({methodArgList});");
                    cb.Line("if (domainResult.IsFailure) return global::Crucible.Chains.Steps.StepOutcome.Failure(domainResult.Errors);");

                    // Handler invocation
                    if (step.HandlerTypeName is { } handler)
                    {
                        var outputArg = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit.Value" : "domainResult.Value";
                        cb.Line($"var handler = (global::Crucible.Chains.Handlers.IStepHandler<global::{aggFqn}, {m.IdTypeName}, {inputType}, {stateType}>)ctx.Services.GetService(typeof(global::Crucible.Chains.Handlers.IStepHandler<global::{aggFqn}, {m.IdTypeName}, {inputType}, {stateType}>))!;");
                        cb.Line($"var handlerResult = await handler.InvokeAsync(aggregate, {inputArg}, {outputArg}, ct).ConfigureAwait(false);");
                        cb.Line("if (handlerResult.IsFailure) return global::Crucible.Chains.Steps.StepOutcome.Failure(handlerResult.Errors);");
                    }

                    // Post processors
                    for (int postIdx = 0; postIdx < step.PostProcessorTypes.Count; postIdx++)
                    {
                        var stateForPost = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit" : step.OutputTypeName!;
                        var outputArg = step.ReturnsResultWithoutValue ? "global::Crucible.Chains.Steps.Unit.Value" : "domainResult.Value";
                        cb.Line($"var post_{postIdx} = (global::Crucible.Chains.Processors.IPostProcessor<global::{aggFqn}, {m.IdTypeName}, {stateForPost}>?)ctx.Services.GetService(typeof(global::Crucible.Chains.Processors.IPostProcessor<global::{aggFqn}, {m.IdTypeName}, {stateForPost}>));");
                        cb.Line($"if (post_{postIdx} is not null)");
                        using (cb.Block())
                        {
                            cb.Line($"var postCtx_{postIdx} = new global::Crucible.Chains.Processors.PostContext<global::{aggFqn}, {m.IdTypeName}, {stateForPost}>(aggregate, {outputArg}, ctx.Services);");
                            cb.Line($"await post_{postIdx}.InvokeAsync(postCtx_{postIdx}, ct).ConfigureAwait(false);");
                        }
                    }

                    if (step.ReturnsResultWithoutValue)
                    {
                        cb.Line("return global::Crucible.Chains.Steps.StepOutcome.Success(global::Crucible.Chains.Steps.Unit.Value);");
                    }
                    else
                    {
                        cb.Line("return global::Crucible.Chains.Steps.StepOutcome.Success((object)domainResult.Value!);");
                    }
                }
            }
        }

        // Reconstruct step impls — one per step
        foreach (var step in m.Steps)
        {
            EmitReconstructStep(cb, m, step, aggFqn);
        }
    }

    private static void EmitReconstructStep(CodeBuilder cb, AggregateModel m, StepModel step, string aggFqn)
    {
        var snapshotIface = $"global::{(string.IsNullOrEmpty(m.Namespace) ? "" : m.Namespace + ".")}I{m.ClassName}Snapshot";

        cb.Line($"internal sealed class __Step_ReconstructAt{step.MethodName} : global::Crucible.Chains.Steps.IStep<global::{aggFqn}, {m.IdTypeName}>");
        using (cb.Block())
        {
            cb.Line($"private readonly {snapshotIface} _snapshot;");
            cb.Line($"public __Step_ReconstructAt{step.MethodName}({snapshotIface} snapshot) {{ this._snapshot = snapshot; }}");
            cb.Line($"public global::Crucible.Chains.Steps.StepKind Kind => global::Crucible.Chains.Steps.StepKind.AggregateMethod;");
            cb.Line($"public string Name => \"{m.ClassName}.ReconstructAt{step.MethodName}\";");
            cb.Line($"public global::System.Threading.Tasks.Task<global::Crucible.Chains.Steps.StepOutcome> InvokeAsync(global::Crucible.Chains.Steps.StepContext<global::{aggFqn}, {m.IdTypeName}> ctx, global::System.Threading.CancellationToken ct)");
            using (cb.Block())
            {
                cb.Line($"var aggregate = new global::{aggFqn}();");
                cb.Line($"aggregate.__HydrateFromSnapshot(this._snapshot);");
                cb.Line($"ctx.Aggregate = aggregate;");
                // No event raised; LastStepResult stays default. The TState slot is the
                // event type only for typestate purposes — the value is never read.
                cb.Line($"return global::System.Threading.Tasks.Task.FromResult(global::Crucible.Chains.Steps.StepOutcome.Success());");
            }
        }
    }
}
