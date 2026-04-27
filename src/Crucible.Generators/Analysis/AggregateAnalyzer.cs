using Crucible.Generators.Diagnostics;
using Crucible.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crucible.Generators.Analysis;

internal sealed record AnalysisResult(AggregateModel? Model, IReadOnlyList<Diagnostic> Diagnostics);

internal static class AggregateAnalyzer
{
    private const string AggregateAttrFqn = "Crucible.Domain.Attributes.AggregateAttribute";
    private const string StepAttrFqn = "Crucible.Domain.Attributes.StepAttribute";
    private const string ResultGenericFqn = "Crucible.Domain.Results.Result<T>";
    private const string ResultFqn = "Crucible.Domain.Results.Result";

    public static AnalysisResult Analyze(INamedTypeSymbol classSymbol, ClassDeclarationSyntax syntax)
    {
        var diagnostics = new List<Diagnostic>();
        var className = classSymbol.Name;

        if (!syntax.Modifiers.Any(m => m.ValueText == "partial"))
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.AggregateNotPartial, syntax.Identifier.GetLocation(), className));
        }

        var idType = ResolveIdType(classSymbol);
        if (idType is null)
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.AggregateNotDerived, syntax.Identifier.GetLocation(), className));
        }

        var stepMethods = classSymbol.GetMembers().OfType<IMethodSymbol>()
            .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == StepAttrFqn))
            .ToArray();

        var steps = new List<StepModel>();
        foreach (var method in stepMethods)
        {
            var attr = method.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == StepAttrFqn);
            int order = (int)(attr.NamedArguments.FirstOrDefault(n => n.Key == "Order").Value.Value ?? 0);
            bool entry = (bool)(attr.NamedArguments.FirstOrDefault(n => n.Key == "Entry").Value.Value ?? false);

            // Read AllowedAfter (string[])
            IReadOnlyList<string>? allowedAfter = null;
            var allowedAfterArg = attr.NamedArguments.FirstOrDefault(n => n.Key == "AllowedAfter");
            if (!allowedAfterArg.Value.IsNull && allowedAfterArg.Value.Values.Length > 0)
            {
                allowedAfter = allowedAfterArg.Value.Values
                    .Select(v => (string)(v.Value ?? ""))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();
            }

            ValidateReturnType(method, syntax, className, diagnostics, out var outputType, out var returnsResultWithoutValue);
            ValidateAsync(method, syntax, className, diagnostics);

            var parameters = method.Parameters
                .Select(p => new ParameterModel(p.Name, p.Type.ToDisplayString()))
                .ToArray();
            var pres = ExtractGenericAttrTypes(method, "Crucible.Domain.Attributes.PreAttribute");
            var posts = ExtractGenericAttrTypes(method, "Crucible.Domain.Attributes.PostAttribute");

            steps.Add(new StepModel(
                method.Name, order, entry,
                outputType, returnsResultWithoutValue,
                parameters, pres, posts,
                HandlerTypeName: null,
                AllowedAfter: allowedAfter));
        }

        ValidateNoPublicConstructors(classSymbol, syntax, className, diagnostics);

        var entryCount = steps.Count(s => s.IsEntry);
        if (entryCount == 0)
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.MissingEntryStep, syntax.Identifier.GetLocation(), className));
        else if (entryCount > 1)
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.MultipleEntrySteps, syntax.Identifier.GetLocation(), className));

        // Detect branching mode: any non-entry step explicitly declared AllowedAfter
        var branchingMode = steps.Any(s => !s.IsEntry && s.AllowedAfter is { Count: > 0 });

        ValidateAllowedAfterGraph(steps, syntax, className, branchingMode, diagnostics);

        // Skip CRC003/CRC004 in branching mode — duplicate Order and gaps are allowed there
        if (!branchingMode)
        {
            ValidateStepOrders(steps, syntax, className, diagnostics);
        }

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return new AnalysisResult(null, diagnostics);

        var aggrAttr = classSymbol.GetAttributes().First(a => a.AttributeClass?.ToDisplayString() == AggregateAttrFqn);
        var entryName = (string?)aggrAttr.NamedArguments.FirstOrDefault(n => n.Key == "EntryName").Value.Value
                        ?? Pluralize(className);

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace ? "" : classSymbol.ContainingNamespace.ToDisplayString();
        var sortedSteps = steps.OrderBy(s => s.Order).ToArray();
        var properties = ExtractHydratableProperties(classSymbol);

        var (children, childDiagnostics) = ExtractEntityChildren(classSymbol, syntax);
        diagnostics.AddRange(childDiagnostics);

        // Normalize: every non-entry step ends up with a non-empty AllowedAfter
        // (in linear mode, infer; in branching mode, the dev provided it)
        var normalizedSteps = NormalizeAllowedAfter(sortedSteps, branchingMode);

        return new AnalysisResult(
            new AggregateModel(ns, className, entryName, idType ?? "global::System.Guid", normalizedSteps, properties, children),
            diagnostics);
    }

    private static IReadOnlyList<StepModel> NormalizeAllowedAfter(
        IReadOnlyList<StepModel> steps,
        bool branchingMode)
    {
        if (branchingMode) return steps;  // dev provided AllowedAfter on each non-entry step

        // Linear mode: infer AllowedAfter for steps that don't have it
        var orderToStep = steps.ToLookup(s => s.Order);
        var result = new List<StepModel>(steps.Count);
        foreach (var step in steps)
        {
            if (step.IsEntry || (step.AllowedAfter is { Count: > 0 }))
            {
                result.Add(step);
                continue;
            }
            var prev = orderToStep[step.Order - 1].FirstOrDefault();
            var inferred = prev is null
                ? System.Array.Empty<string>()
                : new[] { prev.MethodName };
            result.Add(step with { AllowedAfter = inferred });
        }
        return result;
    }

    private static void ValidateAllowedAfterGraph(
        System.Collections.Generic.List<StepModel> steps,
        ClassDeclarationSyntax syntax,
        string className,
        bool branchingMode,
        System.Collections.Generic.List<Diagnostic> diagnostics)
    {
        var stepNames = new System.Collections.Generic.HashSet<string>(steps.Select(s => s.MethodName));

        foreach (var step in steps)
        {
            // Entry step must not have AllowedAfter
            if (step.IsEntry && step.AllowedAfter is { Count: > 0 })
            {
                diagnostics.Add(Diagnostic.Create(
                    CrucibleDiagnostics.EntryStepHasAllowedAfter,
                    syntax.Identifier.GetLocation(),
                    className, step.MethodName));
            }

            // AllowedAfter references must exist
            if (step.AllowedAfter is { } allowed)
            {
                foreach (var pred in allowed)
                {
                    if (!stepNames.Contains(pred))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            CrucibleDiagnostics.AllowedAfterUnknownStep,
                            syntax.Identifier.GetLocation(),
                            className, step.MethodName, pred));
                    }
                }
            }

            // In branching mode, every non-entry step must have AllowedAfter
            if (branchingMode && !step.IsEntry && (step.AllowedAfter is null || step.AllowedAfter.Count == 0))
            {
                diagnostics.Add(Diagnostic.Create(
                    CrucibleDiagnostics.StepHasNoPredecessor,
                    syntax.Identifier.GetLocation(),
                    className, step.MethodName));
            }
        }

        // Cycle detection on the graph defined by AllowedAfter (or implicit linear if not branching)
        var graph = BuildGraph(steps, branchingMode);
        var cycle = FindCycle(graph);
        if (cycle is not null)
        {
            diagnostics.Add(Diagnostic.Create(
                CrucibleDiagnostics.StepGraphHasCycle,
                syntax.Identifier.GetLocation(),
                className, string.Join(" → ", cycle)));
        }
    }

    private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> BuildGraph(
        System.Collections.Generic.List<StepModel> steps,
        bool branchingMode)
    {
        // Edge: predecessor → step (i.e., from each predecessor we can reach this step)
        var graph = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
        foreach (var step in steps) graph[step.MethodName] = new System.Collections.Generic.List<string>();

        var orderToStep = steps.ToLookup(s => s.Order);

        foreach (var step in steps)
        {
            if (step.IsEntry) continue;

            System.Collections.Generic.IEnumerable<string> predecessors;
            if (step.AllowedAfter is { Count: > 0 })
            {
                predecessors = step.AllowedAfter;
            }
            else if (!branchingMode)
            {
                // Linear inference: predecessor = step with Order = current.Order - 1
                var prev = orderToStep[step.Order - 1].FirstOrDefault();
                predecessors = prev is null ? System.Linq.Enumerable.Empty<string>() : new[] { prev.MethodName };
            }
            else
            {
                predecessors = System.Linq.Enumerable.Empty<string>();
            }

            foreach (var pred in predecessors)
            {
                if (graph.TryGetValue(pred, out var list))
                {
                    list.Add(step.MethodName);
                }
            }
        }

        return graph;
    }

    private static System.Collections.Generic.IReadOnlyList<string>? FindCycle(
        System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> graph)
    {
        var visited = new System.Collections.Generic.HashSet<string>();
        var stack = new System.Collections.Generic.HashSet<string>();
        var path = new System.Collections.Generic.List<string>();

        foreach (var node in graph.Keys)
        {
            if (DetectCycleDfs(node, graph, visited, stack, path, out var cycle))
                return cycle;
        }
        return null;
    }

    private static bool DetectCycleDfs(
        string node,
        System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> graph,
        System.Collections.Generic.HashSet<string> visited,
        System.Collections.Generic.HashSet<string> stack,
        System.Collections.Generic.List<string> path,
        out System.Collections.Generic.IReadOnlyList<string>? cycle)
    {
        cycle = null;
        if (stack.Contains(node))
        {
            var startIdx = path.IndexOf(node);
            cycle = path.Skip(startIdx).Concat(new[] { node }).ToArray();
            return true;
        }
        if (visited.Contains(node)) return false;

        visited.Add(node);
        stack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out var neighbors))
        {
            foreach (var next in neighbors)
            {
                if (DetectCycleDfs(next, graph, visited, stack, path, out cycle))
                    return true;
            }
        }

        stack.Remove(node);
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static string? ResolveIdType(INamedTypeSymbol cls)
    {
        for (var b = cls.BaseType; b is not null; b = b.BaseType)
        {
            if (b.OriginalDefinition.ToDisplayString() == "Crucible.Domain.Aggregates.AggregateRoot<TId>")
                return b.TypeArguments[0].ToDisplayString();
        }
        return null;
    }

    private static void ValidateReturnType(IMethodSymbol method, ClassDeclarationSyntax syntax, string cls,
        List<Diagnostic> diagnostics, out string? outputType, out bool returnsResultWithoutValue)
    {
        outputType = null;
        returnsResultWithoutValue = false;

        var ret = method.ReturnType;
        var retName = ret.OriginalDefinition.ToDisplayString();
        if (retName == "Crucible.Domain.Results.Result<T>")
        {
            outputType = ((INamedTypeSymbol)ret).TypeArguments[0].ToDisplayString();
        }
        else if (retName == "Crucible.Domain.Results.Result")
        {
            returnsResultWithoutValue = true;
        }
        else
        {
            diagnostics.Add(Diagnostic.Create(
                CrucibleDiagnostics.StepReturnType,
                method.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                cls, method.Name));
        }
    }

    private static void ValidateAsync(IMethodSymbol method, ClassDeclarationSyntax syntax, string cls, List<Diagnostic> diagnostics)
    {
        if (method.IsAsync || method.ReturnType.Name.StartsWith("Task", System.StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic.Create(
                CrucibleDiagnostics.StepIsAsync,
                method.Locations.FirstOrDefault() ?? syntax.GetLocation(),
                cls, method.Name));
        }
    }

    private static void ValidateStepOrders(List<StepModel> steps, ClassDeclarationSyntax syntax, string cls, List<Diagnostic> diagnostics)
    {
        var orders = steps.Select(s => s.Order).ToArray();
        var seen = new HashSet<int>();
        foreach (var o in orders)
        {
            if (!seen.Add(o))
                diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.DuplicateStepOrder, syntax.Identifier.GetLocation(), cls, o));
        }
        var sorted = orders.Distinct().OrderBy(x => x).ToArray();
        for (int i = 0; i < sorted.Length; i++)
        {
            if (sorted[i] != i + 1)
            {
                diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.StepOrderGap, syntax.Identifier.GetLocation(), cls, i + 1));
                break;
            }
        }
    }

    private static IReadOnlyList<string> ExtractGenericAttrTypes(IMethodSymbol method, string attrOpenFqn)
    {
        var results = new List<string>();
        foreach (var a in method.GetAttributes())
        {
            var ac = a.AttributeClass;
            if (ac is null) continue;
            if (ac.ConstructedFrom.ToDisplayString().StartsWith(attrOpenFqn, System.StringComparison.Ordinal))
            {
                if (ac.TypeArguments.Length == 1)
                    results.Add(ac.TypeArguments[0].ToDisplayString());
            }
        }
        return results;
    }

    private static IReadOnlyList<PropertyModel> ExtractHydratableProperties(INamedTypeSymbol classSymbol)
    {
        var props = new List<PropertyModel>();

        // Aggregate-declared public properties with a setter (any visibility).
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.SetMethod is null) continue;  // read-only props are not hydratable
            if (member.IsStatic) continue;
            // Skip PendingEvents — transient state, not part of the snapshot.
            if (member.Name == "PendingEvents") continue;

            // Skip entity-typed properties — they're tracked as Children, not scalars.
            if (IsEntityRelatedProperty(member.Type)) continue;

            props.Add(new PropertyModel(member.Name, member.Type.ToDisplayString(), PropertyOrigin.Aggregate));
        }

        // Inherited public properties from AggregateRoot<TId>: Id and Version.
        var idProp = classSymbol.BaseType?.GetMembers("Id").OfType<IPropertySymbol>().FirstOrDefault();
        if (idProp is not null)
        {
            props.Add(new PropertyModel("Id", idProp.Type.ToDisplayString(), PropertyOrigin.Base));
        }
        var versionProp = classSymbol.BaseType?.GetMembers("Version").OfType<IPropertySymbol>().FirstOrDefault();
        if (versionProp is not null)
        {
            props.Add(new PropertyModel("Version", versionProp.Type.ToDisplayString(), PropertyOrigin.Base));
        }

        return props;
    }

    private static bool IsEntityRelatedProperty(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named)
        {
            // Single ref?
            if (IsEntityType(named)) return true;
            // Collection?
            if (named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>" &&
                named.TypeArguments.Length == 1 &&
                named.TypeArguments[0] is INamedTypeSymbol elem &&
                IsEntityType(elem))
            {
                return true;
            }
        }
        return false;
    }

    private static (System.Collections.Generic.IReadOnlyList<EntityChildModel> Children, System.Collections.Generic.IReadOnlyList<Diagnostic> Diagnostics) ExtractEntityChildren(
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax syntax)
    {
        var children = new List<EntityChildModel>();
        var diagnostics = new List<Diagnostic>();

        foreach (var prop in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.IsStatic) continue;
            if (prop.Name == "PendingEvents") continue;

            // Detect IReadOnlyList<TEntity>
            if (prop.Type is INamedTypeSymbol named &&
                named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyList<T>" &&
                named.TypeArguments.Length == 1)
            {
                var elementType = named.TypeArguments[0];
                if (elementType is INamedTypeSymbol elementNamed && IsEntityType(elementNamed))
                {
                    var (fieldName, fieldDiag) = ResolveCollectionBackingField(classSymbol, prop, elementNamed, syntax);
                    if (fieldDiag is not null) diagnostics.Add(fieldDiag);
                    children.Add(new EntityChildModel(
                        PropertyName: prop.Name,
                        EntityTypeFqn: elementNamed.ToDisplayString(),
                        EntityClassName: elementNamed.Name,
                        EntityNamespace: elementNamed.ContainingNamespace.IsGlobalNamespace ? "" : elementNamed.ContainingNamespace.ToDisplayString(),
                        Kind: EntityChildKind.Collection,
                        BackingFieldName: fieldName));
                }
                continue;
            }

            // Detect single TEntity reference (with or without nullability)
            if (prop.Type is INamedTypeSymbol singleType && IsEntityType(singleType))
            {
                if (prop.SetMethod is null) continue;  // need a setter for hydration
                children.Add(new EntityChildModel(
                    PropertyName: prop.Name,
                    EntityTypeFqn: singleType.ToDisplayString(),
                    EntityClassName: singleType.Name,
                    EntityNamespace: singleType.ContainingNamespace.IsGlobalNamespace ? "" : singleType.ContainingNamespace.ToDisplayString(),
                    Kind: EntityChildKind.SingleRef,
                    BackingFieldName: null));
            }
        }

        return (children, diagnostics);
    }

    private static bool IsEntityType(INamedTypeSymbol type)
    {
        return type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Crucible.Domain.Attributes.EntityAttribute");
    }

    private static (string? FieldName, Diagnostic? Diagnostic) ResolveCollectionBackingField(
        INamedTypeSymbol classSymbol,
        IPropertySymbol property,
        INamedTypeSymbol elementType,
        ClassDeclarationSyntax syntax)
    {
        // Find private fields of type List<TEntity> or System.Collections.Generic.List<TEntity>
        var candidates = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => !f.IsStatic && !f.IsImplicitlyDeclared)
            .Where(f =>
            {
                if (f.Type is not INamedTypeSymbol named) return false;
                if (named.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.List<T>") return false;
                if (named.TypeArguments.Length != 1) return false;
                return SymbolEqualityComparer.Default.Equals(named.TypeArguments[0], elementType);
            })
            .ToArray();

        if (candidates.Length == 0)
        {
            return (null, Diagnostic.Create(
                CrucibleDiagnostics.MissingChildCollectionField,
                syntax.Identifier.GetLocation(),
                classSymbol.Name, property.Name, elementType.Name));
        }

        if (candidates.Length > 1)
        {
            return (null, Diagnostic.Create(
                CrucibleDiagnostics.AmbiguousChildCollectionField,
                syntax.Identifier.GetLocation(),
                classSymbol.Name, property.Name, elementType.Name));
        }

        return (candidates[0].Name, null);
    }

    private static void ValidateNoPublicConstructors(
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax syntax,
        string className,
        List<Diagnostic> diagnostics)
    {
        foreach (var ctor in classSymbol.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public) continue;

            // For implicit (compiler-synthesized) ctors, point at the class identifier.
            // For explicit ctors, point at the ctor's own location.
            var location = ctor.IsImplicitlyDeclared
                ? syntax.Identifier.GetLocation()
                : ctor.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation();
            diagnostics.Add(Diagnostic.Create(
                CrucibleDiagnostics.AggregateCtorMustNotBePublic,
                location,
                className));
        }
    }

    private static string Pluralize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.EndsWith("y", System.StringComparison.OrdinalIgnoreCase) && s.Length > 1
            && !"aeiou".Contains(char.ToLowerInvariant(s[s.Length - 2])))
            return s.Substring(0, s.Length - 1) + "ies";
        if (s.EndsWith("s", System.StringComparison.OrdinalIgnoreCase) || s.EndsWith("x", System.StringComparison.OrdinalIgnoreCase))
            return s + "es";
        return s + "s";
    }
}
