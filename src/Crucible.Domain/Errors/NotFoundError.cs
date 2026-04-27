namespace Crucible.Domain.Errors;

public sealed record NotFoundError(string ErrorCode, string ErrorDescription) : Error(ErrorCode, ErrorDescription)
{
    public override ErrorKind Kind => ErrorKind.NotFound;
}
