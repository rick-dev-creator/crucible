using Crucible.Generators.Diagnostics;
using Crucible.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crucible.Generators.Analysis;

internal sealed record EntityAnalysisResult(EntityModel? Model, System.Collections.Generic.IReadOnlyList<Diagnostic> Diagnostics);

internal static class EntityAnalyzer
{
    public static EntityAnalysisResult Analyze(INamedTypeSymbol classSymbol, ClassDeclarationSyntax syntax)
    {
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        var className = classSymbol.Name;

        if (!syntax.Modifiers.Any(m => m.ValueText == "partial"))
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.EntityNotPartial, syntax.Identifier.GetLocation(), className));
        }

        var idType = ResolveIdType(classSymbol);
        if (idType is null)
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.EntityNotDerived, syntax.Identifier.GetLocation(), className));
        }

        // Check for parameterless ctor (any visibility — the partial helper invokes it)
        var hasParameterlessCtor = classSymbol.InstanceConstructors.Any(c => c.Parameters.Length == 0);
        if (!hasParameterlessCtor)
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.EntityNoParameterlessCtor, syntax.Identifier.GetLocation(), className));
        }

        ValidateNoPublicConstructors(classSymbol, syntax, className, diagnostics);

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return new EntityAnalysisResult(null, diagnostics);

        var properties = ExtractHydratableProperties(classSymbol);
        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace ? "" : classSymbol.ContainingNamespace.ToDisplayString();

        return new EntityAnalysisResult(
            new EntityModel(ns, className, idType ?? "global::System.Guid", properties),
            diagnostics);
    }

    private static void ValidateNoPublicConstructors(
        INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax syntax,
        string className,
        System.Collections.Generic.List<Diagnostic> diagnostics)
    {
        foreach (var ctor in classSymbol.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public) continue;
            var location = ctor.IsImplicitlyDeclared
                ? syntax.Identifier.GetLocation()
                : ctor.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation();
            diagnostics.Add(Diagnostic.Create(
                CrucibleDiagnostics.EntityCtorMustNotBePublic,
                location,
                className));
        }
    }

    private static string? ResolveIdType(INamedTypeSymbol cls)
    {
        for (var b = cls.BaseType; b is not null; b = b.BaseType)
        {
            if (b.OriginalDefinition.ToDisplayString() == "Crucible.Domain.Aggregates.Entity<TId>")
                return b.TypeArguments[0].ToDisplayString();
        }
        return null;
    }

    private static System.Collections.Generic.IReadOnlyList<PropertyModel> ExtractHydratableProperties(INamedTypeSymbol classSymbol)
    {
        var props = new System.Collections.Generic.List<PropertyModel>();

        // Entity-declared public properties with any setter
        foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility != Accessibility.Public) continue;
            if (member.SetMethod is null) continue;
            if (member.IsStatic) continue;
            props.Add(new PropertyModel(member.Name, member.Type.ToDisplayString(), PropertyOrigin.Aggregate));
        }

        // Inherited Id from Entity<TId>
        var idProp = classSymbol.BaseType?.GetMembers("Id").OfType<IPropertySymbol>().FirstOrDefault();
        if (idProp is not null)
        {
            props.Add(new PropertyModel("Id", idProp.Type.ToDisplayString(), PropertyOrigin.Base));
        }

        return props;
    }
}
