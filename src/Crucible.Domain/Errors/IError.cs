namespace Crucible.Domain.Errors;

/// <summary>
/// Contract for a domain error. The library binds Result&lt;T&gt;, ChainResult&lt;T&gt;,
/// and the chain runtime to this interface so consumers cannot return free-form
/// user-facing messages from the domain. <see cref="ErrorCode"/> identifies the
/// error in a stable, machine-readable form. <see cref="ErrorDescription"/> is for
/// internal logging only — never for end-user presentation. Localized user-facing
/// messages are produced by a presentation layer that maps codes to copy.
/// </summary>
public interface IError
{
    string ErrorCode { get; }
    string ErrorDescription { get; }
    ErrorKind Kind { get; }
}
