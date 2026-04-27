namespace Crucible.Domain.Identifiers;

public interface IAggregateId<TSelf> where TSelf : IAggregateId<TSelf>
{
    static abstract TSelf New();
    static abstract TSelf From(Guid value);
    Guid Value { get; }
}
