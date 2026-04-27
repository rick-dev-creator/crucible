namespace Crucible.Domain.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class PreAttribute<T> : Attribute where T : class { }
