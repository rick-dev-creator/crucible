namespace Crucible.Domain.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class PostAttribute<T> : Attribute where T : class { }
