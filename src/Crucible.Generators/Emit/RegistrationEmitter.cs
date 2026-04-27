using Crucible.Generators.Model;

namespace Crucible.Generators.Emit;

internal static class RegistrationEmitter
{
    public static void Emit(CodeBuilder cb, AggregateModel m)
    {
        var aggFqn = string.IsNullOrEmpty(m.Namespace) ? m.ClassName : $"{m.Namespace}.{m.ClassName}";

        cb.Line($"public static class {m.ClassName}AggregateRegistration");
        using (cb.Block())
        {
            cb.Line($"public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection Add{m.ClassName}Aggregate(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
            using (cb.Block())
            {
                foreach (var step in m.Steps)
                {
                    string inputType = step.Parameters.Count == 1
                        ? step.Parameters[0].TypeName
                        : "global::Crucible.Chains.Steps.Unit";
                    string stateType = step.ReturnsResultWithoutValue
                        ? "global::Crucible.Chains.Steps.Unit"
                        : step.OutputTypeName!;

                    if (step.HandlerTypeName is { } handler)
                    {
                        // TryAddScoped: idempotent — calling AddXxxAggregate() twice or after a manual override is safe.
                        cb.Line($"global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddScoped(services, typeof(global::Crucible.Chains.Handlers.IStepHandler<global::{aggFqn}, {m.IdTypeName}, {inputType}, {stateType}>), typeof(global::{handler}));");
                    }
                    foreach (var preFqn in step.PreProcessorTypes)
                    {
                        cb.Line($"global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped(services, typeof(global::Crucible.Chains.Processors.IPreProcessor<global::{aggFqn}, {m.IdTypeName}, {inputType}>), typeof(global::{preFqn}));");
                    }
                    foreach (var postFqn in step.PostProcessorTypes)
                    {
                        cb.Line($"global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped(services, typeof(global::Crucible.Chains.Processors.IPostProcessor<global::{aggFqn}, {m.IdTypeName}, {stateType}>), typeof(global::{postFqn}));");
                    }
                }
                cb.Line("return services;");
            }
        }
    }
}
