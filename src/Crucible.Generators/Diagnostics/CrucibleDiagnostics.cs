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
}
