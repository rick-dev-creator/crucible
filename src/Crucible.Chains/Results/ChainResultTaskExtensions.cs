using Crucible.Domain.Errors;

namespace Crucible.Chains.Results;

public static class ChainResultTaskExtensions
{
    public static async Task<TOut> Match<T, TOut>(
        this Task<ChainResult<T>> task,
        Func<T, TOut> success,
        Func<IReadOnlyList<Error>, TOut> failure)
    {
        var result = await task.ConfigureAwait(false);
        return result.Match(success, failure);
    }

    public static async Task<ChainResult<T>> Catch<T>(
        this Task<ChainResult<T>> task,
        Func<Exception, IReadOnlyList<Error>> handler)
    {
        var result = await task.ConfigureAwait(false);
        return result.Catch(handler);
    }

    public static async Task<ChainResult<T>> Catch<T>(
        this Task<ChainResult<T>> task,
        Func<Exception, ChainResult<T>> handler)
    {
        var result = await task.ConfigureAwait(false);
        return result.Catch(handler);
    }
}
