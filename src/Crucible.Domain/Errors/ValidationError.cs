namespace Crucible.Domain.Errors;

public sealed record ValidationError(string ErrorCode, string ErrorDescription, string? Field = null) : Error(ErrorCode, ErrorDescription)
{
    public override ErrorKind Kind => ErrorKind.Validation;
}
