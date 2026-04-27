using Crucible.Domain.Errors;

namespace Crucible.Domain.Results;

public readonly struct Result
{
    private static readonly IReadOnlyList<IError> EmptyErrors = Array.Empty<IError>();

    private readonly IReadOnlyList<IError>? _errors;

    private Result(IReadOnlyList<IError>? errors) => _errors = errors;

    public bool IsSuccess => _errors is null;
    public bool IsFailure => _errors is not null;
    public IReadOnlyList<IError> Errors => _errors ?? EmptyErrors;

    public static Result Success() => new(null);
    public static Result Failure(params IError[] errors) => new(errors);
    public static Result Failure(IReadOnlyList<IError> errors) => new(errors);

    // Implicit from concrete Error class (built-in errors: ValidationError, etc.).
    // C# does not permit user-defined conversions from interface types (CS0552),
    // so custom IError impls must use Failure() explicitly.
    public static implicit operator Result(Error error) => Failure(error);
}
