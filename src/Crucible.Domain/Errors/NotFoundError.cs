namespace Crucible.Domain.Errors;

public sealed record NotFoundError(string Code, string Message) : Error(Code, Message)
{
    public override ErrorKind Kind => ErrorKind.NotFound;
}
