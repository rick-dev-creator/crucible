namespace Crucible.Domain.Errors;

public sealed record InfrastructureError(string ErrorCode, string ErrorDescription) : Error(ErrorCode, ErrorDescription)
{
    public override ErrorKind Kind => ErrorKind.Infrastructure;
}
