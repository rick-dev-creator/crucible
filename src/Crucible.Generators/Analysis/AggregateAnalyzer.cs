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
                HandlerTypeName: null));
        }

        ValidateStepOrders(steps, syntax, className, diagnostics);

        var entryCount = steps.Count(s => s.IsEntry);
        if (entryCount == 0)
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.MissingEntryStep, syntax.Identifier.GetLocation(), className));
        else if (entryCount > 1)
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.MultipleEntrySteps, syntax.Identifier.GetLocation(), className));

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

        return new AnalysisResult(
            new AggregateModel(ns, className, entryName, idType ?? "global::System.Guid", sortedSteps, properties, children),
            diagnostics);
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
