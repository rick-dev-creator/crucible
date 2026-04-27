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
