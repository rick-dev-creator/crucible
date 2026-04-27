namespace Crucible.Domain.Aggregates;

public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : IEquatable<TId>
{
    public TId Id { get; protected set; } = default!;

    public bool Equals(Entity<TId>? other) => other is not null && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is Entity<TId> e && Equals(e);

    public override int GetHashCode() => Id!.GetHashCode();
}
