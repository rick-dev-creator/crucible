namespace Crucible.Domain.Errors;

public sealed record ConflictError(string Code, string Message) : Error(Code, Message)
{
    public override ErrorKind Kind => ErrorKind.Conflict;
}
