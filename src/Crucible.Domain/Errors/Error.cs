namespace Crucible.Domain.Errors;

public abstract record Error(string ErrorCode, string ErrorDescription) : IError
{
    public virtual ErrorKind Kind => ErrorKind.Unspecified;
}
