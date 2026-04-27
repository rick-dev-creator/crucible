using Crucible.Domain.Errors;

namespace Crucible.Chains.Steps;

public readonly struct StepOutcome
{
    private readonly IReadOnlyList<Error>? _errors;
    private readonly object? _result;

    private StepOutcome(object? result, IReadOnlyList<Error>? errors)
    {
        _result = result;
        _errors = errors;
    }

    public bool IsSuccess => _errors is null;
    public bool IsFailure => _errors is not null;
    public IReadOnlyList<Error> Errors => _errors ?? Array.Empty<Error>();
    public object? Result => _result;

    public static StepOutcome Success() => new(null, null);
    public static StepOutcome Success(object result) => new(result, null);
    public static StepOutcome Failure(IReadOnlyList<Error> errors) => new(null, errors);
}
