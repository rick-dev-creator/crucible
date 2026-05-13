using System.IO;
using System.Linq;
using Crucible.Generators.Tests.Fixtures;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;
using Xunit;

namespace Crucible.Generators.Tests;

public sealed class CrucibleGeneratorSnapshotTests
{
    [Fact]
    public System.Threading.Tasks.Task GeneratesOrderAggregate()
    {
        var driver = RunGenerator(OrderAggregateInput.Source);
        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }

    [Fact]
    public void EmitsCRC001_WhenNoEntryStep()
    {
        var src = OrderAggregateInput.Source.Replace(", Entry = true", "");
        var driver = RunGenerator(src);
        var diags = driver.GetRunResult().Diagnostics;
        diags.Should().Contain(d => d.Id == "CRC001");
    }

    [Fact]
    public void EmitsCRC002_WhenMultipleEntrySteps()
    {
        // Mark both methods Entry=true
        var src = OrderAggregateInput.Source.Replace(
            "[Step(Order = 2)]",
            "[Step(Order = 2, Entry = true)]");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC002");
    }

    [Fact]
    public void EmitsCRC003_WhenDuplicateOrder()
    {
        var src = OrderAggregateInput.Source.Replace(
            "[Step(Order = 2)]",
            "[Step(Order = 1)]");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC003");
    }

    [Fact]
    public void EmitsCRC004_WhenOrderGap()
    {
        var src = OrderAggregateInput.Source.Replace(
            "[Step(Order = 2)]",
            "[Step(Order = 3)]");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC004");
    }

    [Fact]
    public void EmitsCRC006_WhenAggregateNotDerivedFromAggregateRoot()
    {
        var src = OrderAggregateInput.Source.Replace(
            "public partial class Order : AggregateRoot<OrderId>",
            "public partial class Order");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC006");
    }

    [Fact]
    public void EmitsCRC005_WhenAggregateNotPartial()
    {
        var src = OrderAggregateInput.Source.Replace(
            "public partial class Order",
            "public class Order");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC005");
    }

    [Fact]
    public void EmitsCRC007_WhenStepReturnsNonResult()
    {
        var src = OrderAggregateInput.Source.Replace(
            "public Result<OrderCreated> Create(OrderDto dto)",
            "public int Create(OrderDto dto)").Replace(
            "Id = OrderId.New();\n        Raise(new OrderCreated(Id));\n        return new OrderCreated(Id);",
            "Id = OrderId.New();\n        Raise(new OrderCreated(Id));\n        return 0;");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC007");
    }

    [Fact]
    public void EmitsCRC008_WhenStepIsAsync()
    {
        var src = OrderAggregateInput.Source.Replace(
            "public Result<OrderCreated> Create(OrderDto dto)",
            "public async System.Threading.Tasks.Task<Result<OrderCreated>> Create(OrderDto dto)").Replace(
            "Id = OrderId.New();\n        Raise(new OrderCreated(Id));\n        return new OrderCreated(Id);",
            "await System.Threading.Tasks.Task.Yield();\n        Id = OrderId.New();\n        Raise(new OrderCreated(Id));\n        return new OrderCreated(Id);");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC008");
    }

    [Fact]
    public void EmitsCRC100_WhenStepHasNoHandler()
    {
        // Base fixture has no handlers in scope; CRC100 should fire for both steps as Error.
        var driver = RunGenerator(OrderAggregateInput.Source);
        var diagnostics = driver.GetRunResult().Diagnostics;
        diagnostics.Should().Contain(d => d.Id == "CRC100");
        diagnostics.Where(d => d.Id == "CRC100").Should().OnlyContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void EmitsCRC200_WhenPreProcessorTypeIsInvalid()
    {
        // Inject a class that does NOT implement IPreProcessor and reference it via [Pre<...>].
        var src = OrderAggregateInput.Source
            .Replace(
                "public sealed record OrderDto(string CustomerId);",
                "public sealed record OrderDto(string CustomerId); public sealed class BadPre { }")
            .Replace(
                "[Step(Order = 1, Entry = true)]",
                "[Step(Order = 1, Entry = true)]\n    [Pre<BadPre>]");

        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC200");
    }

    [Fact]
    public System.Threading.Tasks.Task EmitsEntitySnapshot_AndAggregateReferencesIt()
    {
        var src = OrderAggregateInput.Source.Replace(
            "public sealed record OrderDto(string CustomerId);",
            @"public sealed record OrderDto(string CustomerId);

public readonly record struct OrderItemId(System.Guid Value);

[Crucible.Domain.Attributes.Entity]
public partial class OrderItem : Crucible.Domain.Aggregates.Entity<OrderItemId>
{
    public string Sku { get; private set; } = """";
    private OrderItem() { }
}").Replace(
            "[Aggregate]\npublic partial class Order : AggregateRoot<OrderId>\n{",
            @"[Aggregate]
public partial class Order : AggregateRoot<OrderId>
{
    private readonly System.Collections.Generic.List<OrderItem> _items = new();
    public System.Collections.Generic.IReadOnlyList<OrderItem> Items => _items;
");

        var driver = RunGenerator(src);
        return Verifier.Verify(driver).UseDirectory("Snapshots").UseMethodName("EmitsEntitySnapshot_AndAggregateReferencesIt");
    }

    [Fact]
    public void EmitsCRC011_WhenAggregateHasPublicConstructor()
    {
        // Inject an explicit public parameterless ctor.
        var src = OrderAggregateInput.Source.Replace(
            "private Order() { }",
            "public Order() { }");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC011");
    }

    [Fact]
    public void EmitsCRC305_WhenEntityHasPublicConstructor()
    {
        var src = OrderAggregateInput.Source.Replace(
            "public sealed record OrderDto(string CustomerId);",
            @"public sealed record OrderDto(string CustomerId);

[Crucible.Domain.Attributes.Entity]
public partial class BadEntity : Crucible.Domain.Aggregates.Entity<System.Guid>
{
    public BadEntity() { }
}");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC305");
    }

    [Fact]
    public System.Threading.Tasks.Task GeneratesBranchingApprovalWorkflow()
    {
        var driver = RunGenerator(ApprovalWorkflowInput.Source);
        return Verifier.Verify(driver).UseDirectory("Snapshots").UseMethodName("GeneratesBranchingApprovalWorkflow");
    }

    [Fact]
    public void EmitsCRC012_WhenAllowedAfterReferencesUnknownStep()
    {
        var src = ApprovalWorkflowInput.Source.Replace(
            "AllowedAfter = new[] { nameof(Approve) }",
            "AllowedAfter = new[] { \"DoesNotExist\" }");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC012");
    }

    [Fact]
    public void EmitsCRC014_WhenEntryStepHasAllowedAfter()
    {
        var src = ApprovalWorkflowInput.Source.Replace(
            "[Step(Order = 1, Entry = true)]",
            "[Step(Order = 1, Entry = true, AllowedAfter = new[] { \"Approve\" })]");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC014");
    }

    [Fact]
    public void EmitsCRC013_WhenStepGraphHasCycle()
    {
        // Make Approve depend on Place AND Place depend on Approve — cycle
        var src = ApprovalWorkflowInput.Source.Replace(
            "[Step(Order = 2, AllowedAfter = new[] { nameof(Create) })]\n    public Result<OrderApproved> Approve",
            "[Step(Order = 2, AllowedAfter = new[] { nameof(Place) })]\n    public Result<OrderApproved> Approve");
        var driver = RunGenerator(src);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "CRC013");
    }

    private static CSharpGeneratorDriver RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "SampleAssembly",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            ReferenceAssemblies(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var generator = new CrucibleGenerator();
        var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(parseOptions);
        return (CSharpGeneratorDriver)driver.RunGenerators(compilation);
    }

    private static System.Collections.Generic.IEnumerable<MetadataReference> ReferenceAssemblies()
    {
        var trustedAssemblies = ((string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator);
        foreach (var path in trustedAssemblies)
        {
            if (!string.IsNullOrEmpty(path)) yield return MetadataReference.CreateFromFile(path);
        }
        yield return MetadataReference.CreateFromFile(typeof(Crucible.Domain.Aggregates.AggregateRoot<>).Assembly.Location);
    }
}
