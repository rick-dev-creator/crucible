using Crucible.Domain.Errors;

namespace Crucible.Domain.Results;

public readonly struct Result<T>
{
    private static readonly IReadOnlyList<IError> EmptyErrors = Array.Empty<IError>();

    private readonly T? _value;
    private readonly IReadOnlyList<IError>? _errors;

    private Result(T? value, IReadOnlyList<IError>? errors)
    {
        _value = value;
        _errors = errors;
    }

    public bool IsSuccess => _errors is null;
    public bool IsFailure => _errors is not null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result is in a failure state; access Errors instead.");

    public IReadOnlyList<IError> Errors => _errors ?? EmptyErrors;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(params IError[] errors) => new(default, errors);
    public static Result<T> Failure(IReadOnlyList<IError> errors) => new(default, errors);

    public TOut Match<TOut>(Func<T, TOut> success, Func<IReadOnlyList<IError>, TOut> failure)
        => IsSuccess ? success(_value!) : failure(_errors!);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
    public static implicit operator Result<T>(Error[] errors) => Failure(errors);
}
