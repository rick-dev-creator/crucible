using Crucible.Generators.Diagnostics;
using Crucible.Generators.Model;
using Microsoft.CodeAnalysis;

namespace Crucible.Generators.Analysis;

internal static class HandlerResolver
{
    private const string HandlerInterfaceOpenFqn = "Crucible.Chains.Handlers.IStepHandler<TAggregate, TId, TInput, TOutput>";
    private const string PreInterfaceOpenFqn = "Crucible.Chains.Processors.IPreProcessor<TAggregate, TId, TInput>";
    private const string PostInterfaceOpenFqn = "Crucible.Chains.Processors.IPostProcessor<TAggregate, TId, TOutput>";
    private const string UnitFqn = "Crucible.Chains.Steps.Unit";

    public static (IReadOnlyList<StepModel> Steps, IReadOnlyList<Diagnostic> Diagnostics) Resolve(
        AggregateModel model,
        Compilation compilation,
        Location reportLocation)
    {
        var diagnostics = new List<Diagnostic>();
        var allTypes = GetAllTypes(compilation.GlobalNamespace).ToArray();
        var handlerByOutput = new Dictionary<string, List<INamedTypeSymbol>>();
        var typesByFqn = new Dictionary<string, INamedTypeSymbol>();

        var aggregateFqn = string.IsNullOrEmpty(model.Namespace) ? model.ClassName : $"{model.Namespace}.{model.ClassName}";

        foreach (var t in allTypes)
        {
            typesByFqn[t.ToDisplayString()] = t;

            foreach (var iface in t.AllInterfaces)
            {
                if (iface.OriginalDefinition.ToDisplayString() != HandlerInterfaceOpenFqn)
                    continue;
                if (iface.TypeArguments.Length != 4) continue;
                var aggArg = iface.TypeArguments[0].ToDisplayString();
                if (aggArg != aggregateFqn) continue;
                var outputArg = iface.TypeArguments[3].ToDisplayString();
                if (!handlerByOutput.TryGetValue(outputArg, out var list))
                {
                    list = new List<INamedTypeSymbol>();
                    handlerByOutput[outputArg] = list;
                }
                list.Add(t);
            }
        }

        var resolvedSteps = new List<StepModel>();
        foreach (var step in model.Steps)
        {
            ValidateProcessorTypes(step, model, aggregateFqn, typesByFqn, reportLocation, diagnostics);

            if (step.OutputTypeName is null || step.ReturnsResultWithoutValue)
            {
                resolvedSteps.Add(step);
                if (!step.ReturnsResultWithoutValue)
                {
                    diagnostics.Add(Diagnostic.Create(
                        CrucibleDiagnostics.StepHasNoHandler, reportLocation, model.ClassName, step.MethodName));
                }
                continue;
            }

            if (!handlerByOutput.TryGetValue(step.OutputTypeName, out var matches) || matches.Count == 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    CrucibleDiagnostics.StepHasNoHandler, reportLocation, model.ClassName, step.MethodName));
                resolvedSteps.Add(step);
                continue;
            }

            if (matches.Count > 1)
            {
                diagnostics.Add(Diagnostic.Create(
                    CrucibleDiagnostics.AmbiguousHandler, reportLocation, model.ClassName, step.MethodName, model.ClassName));
                resolvedSteps.Add(step);
                continue;
            }

            resolvedSteps.Add(step with { HandlerTypeName = matches[0].ToDisplayString() });
        }

        return (resolvedSteps, diagnostics);
    }

    private static void ValidateProcessorTypes(
        StepModel step,
        AggregateModel model,
        string aggregateFqn,
        Dictionary<string, INamedTypeSymbol> typesByFqn,
        Location reportLocation,
        List<Diagnostic> diagnostics)
    {
        var inputType = step.Parameters.Count == 1 ? step.Parameters[0].TypeName : UnitFqn;
        var outputType = step.ReturnsResultWithoutValue ? UnitFqn : step.OutputTypeName!;

        foreach (var preFqn in step.PreProcessorTypes)
        {
            if (!ImplementsClosedInterface(preFqn, typesByFqn, PreInterfaceOpenFqn, aggregateFqn, model.IdTypeName, inputType))
            {
                var expected = $"IPreProcessor<{aggregateFqn}, {model.IdTypeName}, {inputType}>";
                diagnostics.Add(Diagnostic.Create(
                    CrucibleDiagnostics.InvalidPreOrPostType, reportLocation, preFqn, "Pre", expected));
            }
        }

        foreach (var postFqn in step.PostProcessorTypes)
        {
            if (!ImplementsClosedInterface(postFqn, typesByFqn, PostInterfaceOpenFqn, aggregateFqn, model.IdTypeName, outputType))
            {
                var expected = $"IPostProcessor<{aggregateFqn}, {model.IdTypeName}, {outputType}>";
                diagnostics.Add(Diagnostic.Create(
                    CrucibleDiagnostics.InvalidPreOrPostType, reportLocation, postFqn, "Post", expected));
            }
        }
    }

    private static bool ImplementsClosedInterface(
        string targetTypeFqn,
        Dictionary<string, INamedTypeSymbol> typesByFqn,
        string openInterfaceFqn,
        string expectedAggregateFqn,
        string expectedIdFqn,
        string expectedThirdArgFqn)
    {
        if (!typesByFqn.TryGetValue(targetTypeFqn, out var targetType)) return false;

        foreach (var iface in targetType.AllInterfaces)
        {
            if (iface.OriginalDefinition.ToDisplayString() != openInterfaceFqn) continue;
            if (iface.TypeArguments.Length != 3) continue;
            if (iface.TypeArguments[0].ToDisplayString() != expectedAggregateFqn) continue;
            if (iface.TypeArguments[1].ToDisplayString() != expectedIdFqn) continue;
            if (iface.TypeArguments[2].ToDisplayString() != expectedThirdArgFqn) continue;
            return true;
        }
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers()) yield return t;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in GetAllTypes(sub)) yield return t;
    }
}
