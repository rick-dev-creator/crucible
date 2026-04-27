namespace Crucible.Domain.Errors;

public abstract record Error(string Code, string Message)
{
    public virtual ErrorKind Kind => ErrorKind.Unspecified;
}
