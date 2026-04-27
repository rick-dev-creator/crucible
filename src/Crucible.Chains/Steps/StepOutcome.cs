using Crucible.Domain.Errors;

namespace Crucible.Chains.Steps;

public readonly struct StepOutcome
{
    private readonly IReadOnlyList<IError>? _errors;
    private readonly object? _result;

    private StepOutcome(object? result, IReadOnlyList<IError>? errors)
    {
        _result = result;
        _errors = errors;
    }

    public bool IsSuccess => _errors is null;
    public bool IsFailure => _errors is not null;
    public IReadOnlyList<IError> Errors => _errors ?? Array.Empty<IError>();
    public object? Result => _result;

    public static StepOutcome Success() => new(null, null);
    public static StepOutcome Success(object result) => new(result, null);
    public static StepOutcome Failure(IReadOnlyList<IError> errors) => new(null, errors);
}
