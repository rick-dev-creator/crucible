namespace Crucible.Domain.Aggregates;

public sealed class ValueObjectException : Exception
{
    public ValueObjectException(string message) : base(message) { }
}
