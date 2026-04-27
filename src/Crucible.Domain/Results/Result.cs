using Crucible.Domain.Errors;

namespace Crucible.Domain.Results;

public readonly struct Result
{
    private static readonly IReadOnlyList<Error> EmptyErrors = Array.Empty<Error>();

    private readonly IReadOnlyList<Error>? _errors;

    private Result(IReadOnlyList<Error>? errors) => _errors = errors;

    public bool IsSuccess => _errors is null;
    public bool IsFailure => _errors is not null;
    public IReadOnlyList<Error> Errors => _errors ?? EmptyErrors;

    public static Result Success() => new(null);
    public static Result Failure(params Error[] errors) => new(errors);
    public static Result Failure(IReadOnlyList<Error> errors) => new(errors);

    public static implicit operator Result(Error error) => Failure(error);
}
