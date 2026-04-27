namespace Crucible.Domain.Errors;

public sealed record BusinessRuleError(string ErrorCode, string ErrorDescription) : Error(ErrorCode, ErrorDescription)
{
    public override ErrorKind Kind => ErrorKind.BusinessRule;
}
