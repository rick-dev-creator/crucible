namespace Crucible.Chains.Steps;

public enum StepKind
{
    AggregateMethod,
    Tap,
    OnError,
    ProducedEvents,
    DispatchEvents,
}
