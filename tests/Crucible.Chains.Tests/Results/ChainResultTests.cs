using Crucible.Chains.Results;
using Crucible.Domain.Errors;
using Crucible.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Crucible.Chains.Tests.Results;

public sealed class ChainResultTests
{
    private static readonly IReadOnlyList<IDomainEvent> NoEvents = Array.Empty<IDomainEvent>();

    [Fact]
    public void Success_HasIsSuccessTrue()
    {
        var r = ChainResult<int>.Success(42, NoEvents);
        r.IsSuccess.Should().BeTrue();
        r.IsDomainFailure.Should().BeFalse();
        r.IsExceptional.Should().BeFalse();
    }

    [Fact]
    public void DomainFailure_HasIsDomainFailureTrue()
    {
        var r = ChainResult<int>.DomainFailure(new[] { (Error)new ValidationError("C", "m") }, NoEvents);
        r.IsDomainFailure.Should().BeTrue();
        r.IsSuccess.Should().BeFalse();
        r.IsExceptional.Should().BeFalse();
    }

    [Fact]
    public void Exceptional_HasIsExceptionalTrue()
    {
        var r = ChainResult<int>.Exceptional(new InvalidOperationException("x"), NoEvents);
        r.IsExceptional.Should().BeTrue();
        r.IsSuccess.Should().BeFalse();
        r.IsDomainFailure.Should().BeFalse();
    }

    [Fact]
    public void Match_OnSuccess_InvokesSuccessBranch()
    {
        var r = ChainResult<int>.Success(7, NoEvents);
        r.Match(v => v * 2, _ => -1).Should().Be(14);
    }

    [Fact]
    public void Match_OnDomainFailure_InvokesFailureBranch()
    {
        var r = ChainResult<int>.DomainFailure(new[] { (Error)new ValidationError("C", "m") }, NoEvents);
        r.Match(_ => 0, errs => errs.Count).Should().Be(1);
    }

    [Fact]
    public void Match_OnExceptional_WithoutCatch_Rethrows()
    {
        var r = ChainResult<int>.Exceptional(new InvalidOperationException("boom"), NoEvents);
        Action act = () => r.Match(_ => 0, _ => 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public void Catch_OnExceptional_TranslatesToDomainFailure()
    {
        var r = ChainResult<int>.Exceptional(new InvalidOperationException("boom"), NoEvents);
        var translated = r.Catch(ex => new[] { (Error)new InfrastructureError("X", ex.Message) });
        translated.IsDomainFailure.Should().BeTrue();
        translated.Match(_ => "ok", errs => errs[0].ErrorCode).Should().Be("X");
    }

    [Fact]
    public void Catch_OnSuccess_IsNoOp()
    {
        var r = ChainResult<int>.Success(1, NoEvents);
        var afterCatch = r.Catch(_ => Array.Empty<IError>());
        afterCatch.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ProducedEvents_PreservesOrder()
    {
        var events = new IDomainEvent[] { new TestEvent("a"), new TestEvent("b") };
        var r = ChainResult<int>.Success(0, events);
        r.ProducedEvents.Should().HaveCount(2);
        ((TestEvent)r.ProducedEvents[1]).Tag.Should().Be("b");
    }

    private sealed record TestEvent(string Tag) : DomainEvent;
}
