using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class SnapshotEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        var aggFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

        // 1. Snapshot interface — scalars + child references
        cb.Line($"public interface I{m.ClassName}Snapshot");
        using (cb.Block())
        {
            foreach (var p in m.Properties)
            {
                cb.Line($"{p.TypeName} {p.Name} {{ get; }}");
            }
            foreach (var c in m.Children)
            {
                var childIface = $"global::{(string.IsNullOrEmpty(c.EntityNamespace) ? "" : c.EntityNamespace + ".")}I{c.EntityClassName}Snapshot";
                if (c.Kind == EntityChildKind.Collection)
                {
                    cb.Line($"global::System.Collections.Generic.IReadOnlyList<{childIface}> {c.PropertyName} {{ get; }}");
                }
                else
                {
                    cb.Line($"{childIface}? {c.PropertyName} {{ get; }}");
                }
            }
        }

        cb.Line();

        // 2. Hydration partial — assigns scalars + rehydrates children
        cb.Line($"public partial class {m.ClassName}");
        using (cb.Block())
        {
            cb.Line($"internal static global::{aggFqn} __CreateForChain() => new global::{aggFqn}();");
            cb.Line();
            cb.Line($"internal void __HydrateFromSnapshot(I{m.ClassName}Snapshot snapshot)");
            using (cb.Block())
            {
                foreach (var p in m.Properties)
                {
                    if (p.Origin == PropertyOrigin.Base && p.Name == "Version")
                    {
                        // Version's setter is internal to Crucible.Domain. Use the sanctioned
                        // protected helper RestoreVersion(long).
                        cb.Line($"RestoreVersion(snapshot.Version);");
                    }
                    else
                    {
                        // Id has protected set (accessible from this partial of the subclass).
                        // Aggregate-declared properties have private set (also accessible
                        // because this is a partial of the same class).
                        cb.Line($"{p.Name} = snapshot.{p.Name};");
                    }
                }
                foreach (var c in m.Children)
                {
                    var entityFqn = string.IsNullOrEmpty(c.EntityNamespace) ? c.EntityTypeFqn : $"{c.EntityNamespace}.{c.EntityClassName}";
                    if (c.Kind == EntityChildKind.Collection && c.BackingFieldName is not null)
                    {
                        cb.Line($"this.{c.BackingFieldName}.Clear();");
                        cb.Line($"foreach (var __child in snapshot.{c.PropertyName})");
                        using (cb.Block())
                        {
                            cb.Line($"this.{c.BackingFieldName}.Add(global::{entityFqn}.RehydrateFrom(__child));");
                        }
                    }
                    else if (c.Kind == EntityChildKind.SingleRef)
                    {
                        cb.Line($"{c.PropertyName} = snapshot.{c.PropertyName} is null ? null : global::{entityFqn}.RehydrateFrom(snapshot.{c.PropertyName});");
                    }
                    // If Collection but BackingFieldName is null, the analyzer already emitted CRC303/304;
                    // we silently skip emission for that property.
                }
            }
        }
    }
}
