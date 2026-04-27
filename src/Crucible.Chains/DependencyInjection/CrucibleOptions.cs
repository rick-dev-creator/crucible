using Crucible.Chains.Behaviors;
using Crucible.Chains.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Crucible.Chains.DependencyInjection;

public sealed class CrucibleOptions
{
    private readonly IServiceCollection _services;
    private readonly EventDispatchOptions _eventOpts = new();

    public CrucibleOptions(IServiceCollection services) => _services = services;

    internal EventDispatchOptions EventDispatchOptions => _eventOpts;

    public CrucibleOptions AddBehavior<TBehavior>() where TBehavior : class, IStepBehavior
    {
        _services.AddSingleton<IStepBehavior, TBehavior>();
        return this;
    }

    public CrucibleOptions EventDispatch(Action<EventDispatchOptions> configure)
    {
        configure(_eventOpts);
        return this;
    }
}
