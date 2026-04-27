using Crucible.Domain.Errors;
using Crucible.Domain.Results;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Results;

public sealed class ResultOfTTests
{
    [Fact]
    public void Success_ExposesValue()
    {
        var r = Result<int>.Success(42);
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
        r.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var r = Result<int>.Failure(new ValidationError("C", "m"));
        r.IsSuccess.Should().BeFalse();
        Action act = () => _ = r.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_OnSuccess_InvokesSuccessBranch()
    {
        var r = Result<int>.Success(7);
        r.Match(v => v * 2, _ => -1).Should().Be(14);
    }

    [Fact]
    public void Match_OnFailure_InvokesFailureBranch()
    {
        var r = Result<int>.Failure(new ValidationError("C", "m"));
        r.Match(_ => 0, errs => errs.Count).Should().Be(1);
    }

    [Fact]
    public void ImplicitConversionFromValue_ProducesSuccess()
    {
        Result<int> r = 99;
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(99);
    }

    [Fact]
    public void ImplicitConversionFromError_ProducesFailure()
    {
        Result<int> r = new ValidationError("C", "m");
        r.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ImplicitConversionFromErrorArray_ProducesFailure()
    {
        Result<int> r = new Error[] { new ValidationError("A", "a"), new ValidationError("B", "b") };
        r.IsSuccess.Should().BeFalse();
        r.Errors.Should().HaveCount(2);
    }
}
