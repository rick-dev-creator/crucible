namespace Crucible.Generators.Model;

internal sealed record StepModel(
    string MethodName,
    int Order,
    bool IsEntry,
    string? OutputTypeName,
    bool ReturnsResultWithoutValue,
    System.Collections.Generic.IReadOnlyList<ParameterModel> Parameters,
    System.Collections.Generic.IReadOnlyList<string> PreProcessorTypes,
    System.Collections.Generic.IReadOnlyList<string> PostProcessorTypes,
    string? HandlerTypeName);

internal sealed record ParameterModel(string Name, string TypeName);
