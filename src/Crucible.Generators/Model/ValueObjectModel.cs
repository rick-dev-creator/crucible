namespace Crucible.Generators.Model;

internal sealed record ValueObjectModel(
    string Namespace,
    string ClassName,
    System.Collections.Generic.IReadOnlyList<ValueObjectPropertyModel> Properties);

internal sealed record ValueObjectPropertyModel(string Name, string TypeName);
