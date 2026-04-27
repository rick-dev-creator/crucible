namespace Crucible.Chains.Steps;

public sealed record StepDescriptor(
    string AggregateName,
    string StepName,
    StepKind Kind,
    Type? AggregateType,
    Type? HandlerType);
