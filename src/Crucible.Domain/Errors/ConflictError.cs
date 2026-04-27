namespace Crucible.Domain.Errors;

public sealed record ConflictError(string ErrorCode, string ErrorDescription) : Error(ErrorCode, ErrorDescription)
{
    public override ErrorKind Kind => ErrorKind.Conflict;
}
