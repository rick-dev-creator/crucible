using Crucible.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace Crucible.Domain.Tests.Errors;

public sealed class ErrorTests
{
    [Fact]
    public void ValidationError_Kind_IsValidation()
        => new ValidationError("CODE", "msg").Kind.Should().Be(ErrorKind.Validation);

    [Fact]
    public void BusinessRuleError_Kind_IsBusinessRule()
        => new BusinessRuleError("CODE", "msg").Kind.Should().Be(ErrorKind.BusinessRule);

    [Fact]
    public void ConflictError_Kind_IsConflict()
        => new ConflictError("CODE", "msg").Kind.Should().Be(ErrorKind.Conflict);

    [Fact]
    public void NotFoundError_Kind_IsNotFound()
        => new NotFoundError("CODE", "msg").Kind.Should().Be(ErrorKind.NotFound);

    [Fact]
    public void InfrastructureError_Kind_IsInfrastructure()
        => new InfrastructureError("CODE", "msg").Kind.Should().Be(ErrorKind.Infrastructure);

    [Fact]
    public void ValidationError_OptionalField_DefaultsToNull()
        => new ValidationError("C", "m").Field.Should().BeNull();

    [Fact]
    public void Errors_AreStructurallyEqual_WhenFieldsMatch()
    {
        var a = new ValidationError("C", "m", "field");
        var b = new ValidationError("C", "m", "field");
        a.Should().Be(b);
    }

    [Fact]
    public void Error_BaseHasUnspecifiedKindByDefault()
    {
        Error e = new ValidationError("C", "m");
        e.Code.Should().Be("C");
        e.Message.Should().Be("m");
    }
}
