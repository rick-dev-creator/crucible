namespace Crucible.Domain.Errors;

public sealed record InfrastructureError(string Code, string Message) : Error(Code, Message)
{
    public override ErrorKind Kind => ErrorKind.Infrastructure;
}
