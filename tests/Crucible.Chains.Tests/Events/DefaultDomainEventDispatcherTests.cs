using Crucible.Chains.Events;
using Crucible.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Crucible.Chains.Tests.Events;

public sealed class DefaultDomainEventDispatcherTests
{
    private sealed record SampleEvent(string Tag) : DomainEvent;

    private sealed class TrackingHandler : IDomainEventHandler<SampleEvent>
    {
        public List<string> Seen { get; } = new();
        public Task HandleAsync(SampleEvent @event, CancellationToken ct)
        {
            Seen.Add(@event.Tag);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Sequential_DispatchesAllHandlersInOrder()
    {
        var h1 = new TrackingHandler();
        var h2 = new TrackingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<SampleEvent>>(h1);
        services.AddSingleton<IDomainEventHandler<SampleEvent>>(h2);
        var sp = services.BuildServiceProvider();

        var opts = new EventDispatchOptions { Mode = EventDispatchMode.Sequential };
        var dispatcher = new DefaultDomainEventDispatcher(sp, NullLogger<DefaultDomainEventDispatcher>.Instance, opts);

        await dispatcher.DispatchAsync(new IDomainEvent[] { new SampleEvent("a"), new SampleEvent("b") }, CancellationToken.None);

        h1.Seen.Should().Equal("a", "b");
        h2.Seen.Should().Equal("a", "b");
    }

    [Fact]
    public async Task LogAndContinue_SwallowsHandlerExceptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<SampleEvent>>(new ThrowingHandler());
        var sp = services.BuildServiceProvider();

        var opts = new EventDispatchOptions { OnHandlerError = HandlerErrorPolicy.LogAndContinue };
        var dispatcher = new DefaultDomainEventDispatcher(sp, NullLogger<DefaultDomainEventDispatcher>.Instance, opts);

        Func<Task> act = () => dispatcher.DispatchAsync(new IDomainEvent[] { new SampleEvent("x") }, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Throw_PropagatesHandlerExceptions()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<SampleEvent>>(new ThrowingHandler());
        var sp = services.BuildServiceProvider();

        var opts = new EventDispatchOptions { OnHandlerError = HandlerErrorPolicy.Throw };
        var dispatcher = new DefaultDomainEventDispatcher(sp, NullLogger<DefaultDomainEventDispatcher>.Instance, opts);

        Func<Task> act = () => dispatcher.DispatchAsync(new IDomainEvent[] { new SampleEvent("x") }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class ThrowingHandler : IDomainEventHandler<SampleEvent>
    {
        public Task HandleAsync(SampleEvent @event, CancellationToken ct)
            => throw new InvalidOperationException("handler failed");
    }
}
