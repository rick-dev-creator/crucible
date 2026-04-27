using Microsoft.CodeAnalysis;

namespace Crucible.Generators.Diagnostics;

internal static class CrucibleDiagnostics
{
    private const string Category = "Crucible";
    private const string DocBase = "https://docs.crucible.dev/diagnostics/";

    public static readonly DiagnosticDescriptor MissingEntryStep = new(
        "CRC001", "Aggregate has no entry step",
        "Aggregate '{0}' must have exactly one method marked [Step(Entry = true)]",
        Category, DiagnosticSeverity.Error, isEnabledByDefault: true, helpLinkUri: DocBase + "CRC001");

    public static readonly DiagnosticDescriptor MultipleEntrySteps = new(
        "CRC002", "Aggregate has multiple entry steps",
        "Aggregate '{0}' has multiple methods with [Step(Entry = true)]; only one is allowed",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC002");

    public static readonly DiagnosticDescriptor DuplicateStepOrder = new(
        "CRC003", "Duplicate step order",
        "Aggregate '{0}' has multiple steps with Order = {1}",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC003");

    public static readonly DiagnosticDescriptor StepOrderGap = new(
        "CRC004", "Step order has a gap",
        "Aggregate '{0}' step order has a gap at {1}; orders must be contiguous from 1",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC004");

    public static readonly DiagnosticDescriptor AggregateNotPartial = new(
        "CRC005", "Aggregate is not partial",
        "Aggregate '{0}' must be declared 'partial'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC005");

    public static readonly DiagnosticDescriptor AggregateNotDerived = new(
        "CRC006", "Aggregate must derive from AggregateRoot<TId>",
        "Aggregate '{0}' must derive from AggregateRoot<TId>",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC006");

    public static readonly DiagnosticDescriptor StepReturnType = new(
        "CRC007", "Step method has invalid return type",
        "[Step] method '{0}.{1}' must return Result<T> or Result",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC007");

    public static readonly DiagnosticDescriptor StepIsAsync = new(
        "CRC008", "Step method is async or returns Task",
        "[Step] method '{0}.{1}' must be synchronous; aggregate methods cannot be async or return Task",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC008");

    public static readonly DiagnosticDescriptor AmbiguousHandler = new(
        "CRC010", "Ambiguous handler",
        "Multiple handlers match step '{0}.{1}'; expected exactly one IStepHandler<{2},...> implementation",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC010");

    public static readonly DiagnosticDescriptor StepHasNoHandler = new(
        "CRC100", "Step has no handler — runs as domain-only",
        "Step '{0}.{1}' has no IStepHandler implementation; it will run as domain-only",
        Category, DiagnosticSeverity.Info, true, helpLinkUri: DocBase + "CRC100");

    public static readonly DiagnosticDescriptor InvalidPreOrPostType = new(
        "CRC200", "[Pre<T>] / [Post<T>] target does not implement the expected interface",
        "Type '{0}' referenced by [{1}<{0}>] does not implement {2}",
        Category, DiagnosticSeverity.Warning, true, helpLinkUri: DocBase + "CRC200");

    public static readonly DiagnosticDescriptor EntityNotPartial = new(
        "CRC300", "Entity is not partial",
        "Entity '{0}' must be declared 'partial'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC300");

    public static readonly DiagnosticDescriptor EntityNotDerived = new(
        "CRC301", "Entity must derive from Entity<TId>",
        "Entity '{0}' must derive from Entity<TId>",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC301");

    public static readonly DiagnosticDescriptor EntityNoParameterlessCtor = new(
        "CRC302", "Entity must have a parameterless constructor",
        "Entity '{0}' must have a parameterless constructor (any visibility) for hydration",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC302");

    public static readonly DiagnosticDescriptor MissingChildCollectionField = new(
        "CRC303", "No backing field found for entity collection",
        "Aggregate '{0}' property '{1}' of type IReadOnlyList<{2}> requires a private field of type List<{2}> for hydration",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC303");

    public static readonly DiagnosticDescriptor AmbiguousChildCollectionField = new(
        "CRC304", "Multiple candidate backing fields for entity collection",
        "Aggregate '{0}' has multiple fields of type List<{2}>; cannot determine which backs property '{1}'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC304");

    public static readonly DiagnosticDescriptor AggregateCtorMustNotBePublic = new(
        "CRC011", "Aggregate must not have public constructors",
        "Aggregate '{0}' must not have public constructors; aggregates are constructed only through the chain entry (e.g., Orders.Create or Orders.ReconstructAt[Step]). Use private or internal visibility.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC011");

    public static readonly DiagnosticDescriptor EntityCtorMustNotBePublic = new(
        "CRC305", "Entity must not have public constructors",
        "Entity '{0}' must not have public constructors; entities are constructed by their aggregate or rehydrated via {0}.RehydrateFrom. Use private or internal visibility.",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC305");

    public static readonly DiagnosticDescriptor ValueObjectMustBeSealedPartialRecord = new(
        "CRC400", "ValueObject must be a sealed partial record",
        "ValueObject '{0}' must be declared as 'sealed partial record'",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC400");

    public static readonly DiagnosticDescriptor ValueObjectMustDeriveFromBase = new(
        "CRC401", "ValueObject must derive from ValueObject base record",
        "ValueObject '{0}' must derive from Crucible.Domain.Aggregates.ValueObject",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC401");

    public static readonly DiagnosticDescriptor ValueObjectCtorMustNotBePublic = new(
        "CRC402", "ValueObject must not have public constructors",
        "ValueObject '{0}' must not have public constructors; use the generated 'Create' factory. Construction goes through Result<{0}>.Create(...).",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC402");

    public static readonly DiagnosticDescriptor ValueObjectMustHavePrivateParameterlessCtor = new(
        "CRC403", "ValueObject must have a private parameterless constructor",
        "ValueObject '{0}' must declare a 'private {0}() {{ }}' constructor — required for the generated factory and for EF Core materialization of owned types",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC403");

    public static readonly DiagnosticDescriptor ValueObjectPropertiesMustBeInit = new(
        "CRC404", "ValueObject properties must be init-only",
        "ValueObject '{0}' property '{1}' must use 'init' setter (not 'set' or no setter at all)",
        Category, DiagnosticSeverity.Error, true, helpLinkUri: DocBase + "CRC404");
}
