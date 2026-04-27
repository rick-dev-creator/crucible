namespace Crucible.Chains.Events;

public enum EventDispatchMode { Sequential, Parallel }
public enum HandlerErrorPolicy { LogAndContinue, Throw }

public sealed class EventDispatchOptions
{
    public EventDispatchMode Mode { get; set; } = EventDispatchMode.Sequential;
    public HandlerErrorPolicy OnHandlerError { get; set; } = HandlerErrorPolicy.LogAndContinue;
}
