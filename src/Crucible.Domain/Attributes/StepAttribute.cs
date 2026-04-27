namespace Crucible.Domain.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class StepAttribute : Attribute
{
    public int Order { get; init; }
    public bool Entry { get; init; }
    public string[]? AllowedAfter { get; init; }
}
