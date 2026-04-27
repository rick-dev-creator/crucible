namespace Crucible.Domain.Errors;

public sealed record BusinessRuleError(string Code, string Message) : Error(Code, Message)
{
    public override ErrorKind Kind => ErrorKind.BusinessRule;
}
