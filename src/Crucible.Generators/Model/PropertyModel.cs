namespace Crucible.Generators.Model;

internal sealed record PropertyModel(
    string Name,
    string TypeName,
    PropertyOrigin Origin);

internal enum PropertyOrigin
{
    /// <summary>Declared on the aggregate itself.</summary>
    Aggregate,
    /// <summary>Inherited from AggregateRoot (Id, Version).</summary>
    Base,
}
