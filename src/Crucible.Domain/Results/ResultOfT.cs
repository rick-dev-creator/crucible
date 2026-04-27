using Crucible.Domain.Errors;

namespace Crucible.Domain.Results;

public readonly struct Result<T>
{
    private static readonly IReadOnlyList<Error> EmptyErrors = Array.Empty<Error>();

    private readonly T? _value;
    private readonly IReadOnlyList<Error>? _errors;

    private Result(T? value, IReadOnlyList<Error>? errors)
    {
        _value = value;
        _errors = errors;
    }

    public bool IsSuccess => _errors is null;
    public bool IsFailure => _errors is not null;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result is in a failure state; access Errors instead.");

    public IReadOnlyList<Error> Errors => _errors ?? EmptyErrors;

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(params Error[] errors) => new(default, errors);
    public static Result<T> Failure(IReadOnlyList<Error> errors) => new(default, errors);

    public TOut Match<TOut>(Func<T, TOut> success, Func<IReadOnlyList<Error>, TOut> failure)
        => IsSuccess ? success(_value!) : failure(_errors!);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
    public static implicit operator Result<T>(Error[] errors) => Failure(errors);
}
