namespace Crucible.Domain.Errors;

public sealed record ValidationError(string Code, string Message, string? Field = null) : Error(Code, Message)
{
    public override ErrorKind Kind => ErrorKind.Validation;
}
