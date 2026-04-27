namespace Crucible.Domain.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AggregateAttribute : Attribute
{
    public string? EntryName { get; init; }
}
