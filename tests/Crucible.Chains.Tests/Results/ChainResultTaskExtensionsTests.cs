using Crucible.Chains.Results;
using Crucible.Domain.Errors;
using Crucible.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Crucible.Chains.Tests.Results;

public sealed class ChainResultTaskExtensionsTests
{
    private static readonly IReadOnlyList<IDomainEvent> NoEvents = Array.Empty<IDomainEvent>();

    [Fact]
    public async Task Match_OnTaskSuccess_RunsSuccessBranch()
    {
        var task = Task.FromResult(ChainResult<int>.Success(5, NoEvents));
        var result = await task.Match(v => v + 1, _ => -1);
        result.Should().Be(6);
    }

    [Fact]
    public async Task Match_OnTaskDomainFailure_RunsFailureBranch()
    {
        var task = Task.FromResult(ChainResult<int>.DomainFailure(new[] { (Error)new ValidationError("C", "m") }, NoEvents));
        var result = await task.Match(_ => 0, errs => errs.Count);
        result.Should().Be(1);
    }

    [Fact]
    public async Task Catch_OnTaskExceptional_TranslatesAndAllowsMatch()
    {
        var task = Task.FromResult(ChainResult<int>.Exceptional(new InvalidOperationException("boom"), NoEvents));
        var result = await task
            .Catch(ex => new[] { (Error)new InfrastructureError("CODE", ex.Message) })
            .Match(_ => "ok", errs => errs[0].ErrorCode);
        result.Should().Be("CODE");
    }
}
