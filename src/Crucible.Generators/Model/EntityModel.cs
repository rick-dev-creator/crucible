namespace Crucible.Generators.Model;

internal sealed record EntityModel(
    string Namespace,
    string ClassName,
    string IdTypeName,
    System.Collections.Generic.IReadOnlyList<PropertyModel> Properties);
