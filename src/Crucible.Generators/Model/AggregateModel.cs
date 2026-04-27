namespace Crucible.Generators.Model;

internal sealed record AggregateModel(
    string Namespace,
    string ClassName,
    string EntryClassName,
    string IdTypeName,
    System.Collections.Generic.IReadOnlyList<StepModel> Steps,
    System.Collections.Generic.IReadOnlyList<PropertyModel> Properties);
