using System.Collections.Concurrent;
using System.Reflection;
using Crucible.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crucible.Chains.Events;

public sealed class DefaultDomainEventDispatcher : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleAsyncCache = new();

    private readonly IServiceProvider _services;
    private readonly ILogger<DefaultDomainEventDispatcher> _log;
    private readonly EventDispatchOptions _opts;

    public DefaultDomainEventDispatcher(
        IServiceProvider services,
        ILogger<DefaultDomainEventDispatcher> log,
        EventDispatchOptions opts)
    {
        _services = services;
        _log = log;
        _opts = opts;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var @event in events)
        {
            ct.ThrowIfCancellationRequested();
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(@event.GetType());
            var handlers = ((IEnumerable<object>)_services.GetServices(handlerType)).ToArray();
            var method = HandleAsyncCache.GetOrAdd(@event.GetType(),
                t => typeof(IDomainEventHandler<>).MakeGenericType(t).GetMethod("HandleAsync")!);

            if (_opts.Mode == EventDispatchMode.Sequential)
            {
                foreach (var h in handlers)
                {
                    await InvokeAsync(h, method, @event, ct).ConfigureAwait(false);
                }
            }
            else
            {
                await Task.WhenAll(handlers.Select(h => InvokeAsync(h, method, @event, ct))).ConfigureAwait(false);
            }
        }
    }

    private async Task InvokeAsync(object handler, MethodInfo method, IDomainEvent @event, CancellationToken ct)
    {
        try
        {
            var task = (Task)method.Invoke(handler, new object[] { @event, ct })!;
            await task.ConfigureAwait(false);
        }
        catch (TargetInvocationException tie) when (_opts.OnHandlerError == HandlerErrorPolicy.LogAndContinue)
        {
            _log.LogError(tie.InnerException ?? tie, "Event handler {Handler} failed for {Event}", handler.GetType().Name, @event.GetType().Name);
        }
        catch (Exception ex) when (_opts.OnHandlerError == HandlerErrorPolicy.LogAndContinue)
        {
            _log.LogError(ex, "Event handler {Handler} failed for {Event}", handler.GetType().Name, @event.GetType().Name);
        }
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }
    }
}
