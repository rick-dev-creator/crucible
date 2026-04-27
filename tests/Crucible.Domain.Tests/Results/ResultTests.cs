using Crucible.Domain.Errors;
using Crucible.Domain.Results;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_HasNoErrors()
    {
        var r = Result.Success();
        r.IsSuccess.Should().BeTrue();
        r.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failure_CarriesErrors()
    {
        var err = new ValidationError("C", "m");
        var r = Result.Failure(err);
        r.IsSuccess.Should().BeFalse();
        r.Errors.Should().ContainSingle().Which.Should().Be(err);
    }

    [Fact]
    public void ImplicitConversionFromError_ProducesFailure()
    {
        Result r = new ValidationError("C", "m");
        r.IsSuccess.Should().BeFalse();
        r.Errors.Should().HaveCount(1);
    }
}
