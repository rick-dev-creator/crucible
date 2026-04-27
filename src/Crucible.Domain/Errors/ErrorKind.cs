namespace Crucible.Domain.Errors;

public enum ErrorKind
{
    Unspecified,
    Validation,
    BusinessRule,
    Conflict,
    NotFound,
    Infrastructure,
}
