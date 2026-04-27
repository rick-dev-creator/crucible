using Crucible.Generators.Diagnostics;
using Crucible.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crucible.Generators.Analysis;

internal sealed record ValueObjectAnalysisResult(ValueObjectModel? Model, System.Collections.Generic.IReadOnlyList<Diagnostic> Diagnostics);

internal static class ValueObjectAnalyzer
{
    public static ValueObjectAnalysisResult Analyze(INamedTypeSymbol classSymbol, TypeDeclarationSyntax syntax)
    {
        var diagnostics = new System.Collections.Generic.List<Diagnostic>();
        var className = classSymbol.Name;

        // Must be sealed + partial + record (record class — record struct not supported in v2.1)
        var modifiers = syntax.Modifiers.Select(m => m.ValueText).ToArray();
        var isSealedPartialRecord = modifiers.Contains("sealed")
            && modifiers.Contains("partial")
            && classSymbol.IsRecord;
        if (!isSealedPartialRecord)
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.ValueObjectMustBeSealedPartialRecord, syntax.Identifier.GetLocation(), className));
        }

        // Must derive from ValueObject
        if (!DerivesFromValueObject(classSymbol))
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.ValueObjectMustDeriveFromBase, syntax.Identifier.GetLocation(), className));
        }

        // No public ctors
        foreach (var ctor in classSymbol.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility != Accessibility.Public) continue;
            // Skip implicit ctors emitted by the record primary constructor when there's no positional list
            if (ctor.IsImplicitlyDeclared && ctor.Parameters.Length == 0) continue;
            var location = ctor.IsImplicitlyDeclared
                ? syntax.Identifier.GetLocation()
                : ctor.Locations.FirstOrDefault() ?? syntax.Identifier.GetLocation();
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.ValueObjectCtorMustNotBePublic, location, className));
        }

        // Must have explicit private parameterless ctor (not just implicit one)
        var hasPrivateParameterlessCtor = classSymbol.InstanceConstructors.Any(c =>
            c.Parameters.Length == 0
            && c.DeclaredAccessibility == Accessibility.Private
            && !c.IsImplicitlyDeclared);
        if (!hasPrivateParameterlessCtor)
        {
            diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.ValueObjectMustHavePrivateParameterlessCtor, syntax.Identifier.GetLocation(), className));
        }

        // Validate properties: public + init-only
        var properties = new System.Collections.Generic.List<ValueObjectPropertyModel>();
        foreach (var prop in classSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.DeclaredAccessibility != Accessibility.Public) continue;
            if (prop.IsStatic) continue;
            if (prop.IsImplicitlyDeclared) continue;  // skip record-synthesized members
            // Inherited props from ValueObject base — there are none, but just in case
            if (prop.ContainingType?.Name == "ValueObject") continue;

            // Property must have init-only setter
            if (prop.SetMethod is null || !prop.SetMethod.IsInitOnly)
            {
                diagnostics.Add(Diagnostic.Create(CrucibleDiagnostics.ValueObjectPropertiesMustBeInit, syntax.Identifier.GetLocation(), className, prop.Name));
                continue;
            }

            properties.Add(new ValueObjectPropertyModel(prop.Name, prop.Type.ToDisplayString()));
        }

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            return new ValueObjectAnalysisResult(null, diagnostics);

        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace ? "" : classSymbol.ContainingNamespace.ToDisplayString();
        return new ValueObjectAnalysisResult(new ValueObjectModel(ns, className, properties), diagnostics);
    }

    private static bool DerivesFromValueObject(INamedTypeSymbol cls)
    {
        for (var b = cls.BaseType; b is not null; b = b.BaseType)
        {
            if (b.ToDisplayString() == "Crucible.Domain.Aggregates.ValueObject") return true;
        }
        return false;
    }
}
