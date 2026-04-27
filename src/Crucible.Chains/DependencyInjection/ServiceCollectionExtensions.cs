using Crucible.Chains.Behaviors;
using Crucible.Chains.Events;
using Crucible.Chains.Execution;
using Crucible.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crucible.Chains.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCrucible(this IServiceCollection services, Action<CrucibleOptions>? configure = null)
    {
        var options = new CrucibleOptions(services);
        configure?.Invoke(options);

        services.TryAddSingleton(options.EventDispatchOptions);
        services.TryAddSingleton<StepBehaviorPipeline>(sp => new StepBehaviorPipeline(sp.GetServices<IStepBehavior>()));
        services.TryAddSingleton<IChainExecutor, DefaultChainExecutor>();
        services.TryAddScoped<IDomainEventDispatcher, DefaultDomainEventDispatcher>();
        return services;
    }

    public static IServiceCollection AddCrucibleEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IDomainEvent
        where THandler : class, IDomainEventHandler<TEvent>
    {
        services.AddScoped<IDomainEventHandler<TEvent>, THandler>();
        return services;
    }
}
