using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class SnapshotEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        // 1. The snapshot interface
        cb.Line($"public interface I{m.ClassName}Snapshot");
        using (cb.Block())
        {
            foreach (var p in m.Properties)
            {
                cb.Line($"{p.TypeName} {p.Name} {{ get; }}");
            }
        }

        cb.Line();

        // 2. The hydration partial — a partial of the consumer's aggregate class.
        // This is emitted in the consumer's assembly so it has access to private
        // setters of the aggregate's own properties via the partial-class mechanism.
        cb.Line($"public partial class {m.ClassName}");
        using (cb.Block())
        {
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
            }
        }
    }
}
