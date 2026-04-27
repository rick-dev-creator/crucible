using Crucible.Generators.Diagnostics;
using Crucible.Generators.Model;
using Microsoft.CodeAnalysis;

namespace Crucible.Generators.Analysis;

internal static class HandlerResolver
{
    private const string HandlerInterfaceOpenFqn = "Crucible.Chains.Handlers.IStepHandler<TAggregate, TId, TInput, TOutput>";

    public static (IReadOnlyList<StepModel> Steps, IReadOnlyList<Diagnostic> Diagnostics) Resolve(
        AggregateModel model,
        Compilation compilation,
        Location reportLocation)
    {
        var diagnostics = new List<Diagnostic>();
        var allTypes = GetAllTypes(compilation.GlobalNamespace).ToArray();
        var handlerByOutput = new Dictionary<string, List<INamedTypeSymbol>>();

        var aggregateFqn = string.IsNullOrEmpty(model.Namespace) ? model.ClassName : $"{model.Namespace}.{model.ClassName}";

        foreach (var t in allTypes)
        {
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

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers()) yield return t;
        foreach (var sub in ns.GetNamespaceMembers())
            foreach (var t in GetAllTypes(sub)) yield return t;
    }
}
