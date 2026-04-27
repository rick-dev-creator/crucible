namespace Crucible.Generators.Model;

internal sealed record AggregateModel(
    string Namespace,
    string ClassName,
    string EntryClassName,
    string IdTypeName,
    System.Collections.Generic.IReadOnlyList<StepModel> Steps,
    System.Collections.Generic.IReadOnlyList<PropertyModel> Properties,
    System.Collections.Generic.IReadOnlyList<EntityChildModel> Children);

internal sealed record EntityChildModel(
    string PropertyName,
    string EntityTypeFqn,            // e.g., "Sample.OrderItem"
    string EntityClassName,          // e.g., "OrderItem" (for the snapshot interface name and RehydrateFrom call)
    string EntityNamespace,          // e.g., "Sample" (or empty)
    EntityChildKind Kind,
    string? BackingFieldName);       // only for collection — the private field name like "_items"

internal enum EntityChildKind { Collection, SingleRef }
