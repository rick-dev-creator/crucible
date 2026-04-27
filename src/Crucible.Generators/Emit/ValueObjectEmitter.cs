using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class ValueObjectEmitter
{
    public static void Emit(CodeBuilder cb, ValueObjectModel m)
    {
        var voFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";
        var paramsList = string.Join(", ", m.Properties.Select(p => $"{p.TypeName} {ToCamelCase(p.Name)}"));
        var initList = string.Join(", ", m.Properties.Select(p => $"{p.Name} = {ToCamelCase(p.Name)}"));
        var validateArgs = string.Join(", ", m.Properties.Select(p => ToCamelCase(p.Name)));

        cb.Line($"public sealed partial record {m.ClassName}");
        using (cb.Block())
        {
            // Declaration of the partial validation method — dev MUST implement it.
            // Returns Result (no value): success means construction is valid;
            // failure carries the IError list to surface in Result<TVO>.
            cb.Line($"private static partial global::Crucible.Domain.Results.Result __ValidateConstruction({paramsList});");

            cb.Line();
            cb.Line($"public static global::Crucible.Domain.Results.Result<global::{voFqn}> Create({paramsList})");
            using (cb.Block())
            {
                cb.Line($"var __validation = __ValidateConstruction({validateArgs});");
                cb.Line($"if (__validation.IsFailure) return global::Crucible.Domain.Results.Result<global::{voFqn}>.Failure(__validation.Errors);");
                cb.Line($"return new global::{voFqn} {{ {initList} }};");
            }
        }
    }

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (char.IsLower(s[0])) return s;
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
