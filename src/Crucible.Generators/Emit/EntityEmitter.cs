using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class EntityEmitter
{
    public static void Emit(CodeBuilder cb, EntityModel m)
    {
        var entityFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

        // 1. Snapshot interface
        cb.Line($"public interface I{m.ClassName}Snapshot");
        using (cb.Block())
        {
            foreach (var p in m.Properties)
            {
                cb.Line($"{p.TypeName} {p.Name} {{ get; }}");
            }
        }

        cb.Line();

        // 2. Hydration partial — handles property assignment
        cb.Line($"public partial class {m.ClassName}");
        using (cb.Block())
        {
            cb.Line($"internal void __HydrateFromSnapshot(I{m.ClassName}Snapshot snapshot)");
            using (cb.Block())
            {
                foreach (var p in m.Properties)
                {
                    // Id has protected set on Entity<TId> (accessible from this partial of subclass).
                    // Entity-declared properties have private set (accessible from this partial).
                    cb.Line($"{p.Name} = snapshot.{p.Name};");
                }
            }

            cb.Line();
            cb.Line($"public static global::{entityFqn} RehydrateFrom(I{m.ClassName}Snapshot snapshot)");
            using (cb.Block())
            {
                cb.Line($"var entity = new global::{entityFqn}();");
                cb.Line($"entity.__HydrateFromSnapshot(snapshot);");
                cb.Line($"return entity;");
            }
        }
    }
}
