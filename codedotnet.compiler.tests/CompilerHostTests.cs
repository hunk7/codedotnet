using CodeDotNet.Compiler;
using Xunit;

namespace CodeDotNet.Compiler.Tests;

/// <summary>
/// Verifies the curated reference surface and diagnostic behavior of <see cref="CompilerHost"/>
/// (FR-041 through FR-051): supported namespaces, diagnostic shape, and invalid/empty source
/// handling.
/// </summary>
public class CompilerHostTests
{
    [Fact]
    public void Compile_SimpleHelloWorld_Succeeds()
    {
        const string source = """
            using System;
            Console.WriteLine("Hello from codedotnet!");
            """;

        var result = CompilerHost.Compile(source);

        Assert.True(result.Success);
        Assert.NotNull(result.AssemblyBytes);
        Assert.NotEmpty(result.AssemblyBytes!);
    }

    [Theory]
    [InlineData("using System.Collections.Generic; var list = new List<int> { 1, 2, 3 }; System.Console.WriteLine(list.Count);")]
    [InlineData("using System.Linq; var nums = new[] { 1, 2, 3 }; System.Console.WriteLine(nums.Sum());")]
    [InlineData("using System.Text; var sb = new StringBuilder(); sb.Append(\"ok\"); System.Console.WriteLine(sb.ToString());")]
    [InlineData("using System.Text.Json; System.Console.WriteLine(JsonSerializer.Serialize(new[] { 1, 2 }));")]
    [InlineData("using System.Threading.Tasks; await Task.Delay(1); System.Console.WriteLine(\"done\");")]
    [InlineData("using System.Numerics; var v = new Vector2(1, 2); System.Console.WriteLine(v.X);")]
    public void Compile_SupportedReferenceSurface_Succeeds(string source)
    {
        var result = CompilerHost.Compile(source);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
    }

    [Fact]
    public void Compile_SyntaxError_ProducesDiagnosticWithLineAndColumn()
    {
        const string source = """
            using System;
            Console.WriteLine("missing semicolon")
            """;

        var result = CompilerHost.Compile(source);

        Assert.False(result.Success);
        Assert.Null(result.AssemblyBytes);
        Assert.Contains(result.Diagnostics, d => d.Severity == "Error");

        var diagnostic = result.Diagnostics.First(d => d.Severity == "Error");
        Assert.False(string.IsNullOrEmpty(diagnostic.Id));
        Assert.False(string.IsNullOrEmpty(diagnostic.Message));
        Assert.True(diagnostic.StartLine >= 1);
        Assert.True(diagnostic.StartColumn >= 1);
        Assert.True(diagnostic.EndLine >= diagnostic.StartLine);
    }

    [Fact]
    public void Compile_EmptySource_ProducesDiagnosticsNotInternalError()
    {
        var result = CompilerHost.Compile(string.Empty);

        // Empty top-level-statement source has no entry point; this should surface as a
        // compiler diagnostic rather than throwing (FR-051).
        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Compile_UndeclaredIdentifier_ProducesAccurateDiagnostic()
    {
        const string source = """
            using System;
            Console.WriteLine(doesNotExist);
            """;

        var result = CompilerHost.Compile(source);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS0103");
    }

    [Fact]
    public void Compile_WarningOnly_StillSucceeds()
    {
        const string source = """
            using System;
            int unused = 42;
            Console.WriteLine("ok");
            """;

        var result = CompilerHost.Compile(source);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == "Warning");
    }

    [Fact]
    public void Compile_RunsRepeatedlyWithoutInternalCompilerError()
    {
        // Regression for the first-compile crash where a Lazy<T>-cached reference list
        // built from Assembly.Location (empty on browser-wasm, since assemblies are loaded
        // from an in-memory bundle rather than disk) would throw once via
        // MetadataReference.CreateFromFile("") and then keep surfacing as CDN000
        // "Internal compiler error" on every subsequent call. References are now built
        // directly from in-memory assembly metadata (Assembly.TryGetRawMetadata), which does
        // not depend on a backing file path at all.
        const string source = """
            using System;
            Console.WriteLine("Hello, codedotnet!");
            """;

        for (var i = 0; i < 3; i++)
        {
            var result = CompilerHost.Compile(source);

            Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
            Assert.NotNull(result.AssemblyBytes);
        }
    }
}
