using Crucible.Domain.Errors;
using Crucible.Domain.Events;

namespace Crucible.Chains.Results;

public readonly struct ChainResult<T>
{
    private readonly T? _value;
    private readonly IReadOnlyList<Error>? _errors;
    private readonly Exception? _exception;
    private readonly IReadOnlyList<IDomainEvent> _events;

    private ChainResult(T? value, IReadOnlyList<Error>? errors, Exception? exception, IReadOnlyList<IDomainEvent> events)
    {
        _value = value;
        _errors = errors;
        _exception = exception;
        _events = events;
    }

    public bool IsSuccess => _errors is null && _exception is null;
    public bool IsDomainFailure => _errors is not null;
    public bool IsExceptional => _exception is not null;

    public IReadOnlyList<IDomainEvent> ProducedEvents => _events;

    public static ChainResult<T> Success(T value, IReadOnlyList<IDomainEvent> events)
        => new(value, null, null, events);

    public static ChainResult<T> DomainFailure(IReadOnlyList<Error> errors, IReadOnlyList<IDomainEvent> events)
        => new(default, errors, null, events);

    public static ChainResult<T> Exceptional(Exception exception, IReadOnlyList<IDomainEvent> events)
        => new(default, null, exception, events);

    public TOut Match<TOut>(Func<T, TOut> success, Func<IReadOnlyList<Error>, TOut> failure)
    {
        if (_exception is not null) throw _exception;
        return _errors is null ? success(_value!) : failure(_errors);
    }

    public ChainResult<T> Catch(Func<Exception, IReadOnlyList<Error>> handler)
    {
        if (_exception is null) return this;
        return DomainFailure(handler(_exception), _events);
    }

    public ChainResult<T> Catch(Func<Exception, ChainResult<T>> handler)
    {
        if (_exception is null) return this;
        return handler(_exception);
    }
}
