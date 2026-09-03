using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeDotNet.Compiler;

/// <summary>
/// Configures and runs Roslyn compilation of a single <c>Program.cs</c> source file for the
/// browser worker, per FR-041 through FR-051.
/// </summary>
internal static class CompilerHost
{
    private const string AssemblyName = "codedotnet-user-program";

    /// <summary>Curated reference assemblies supporting the FR-043 surface area.</summary>
    private static readonly Lazy<MetadataReference[]> References = new(BuildReferences);

    public static CompileResult Compile(string source)
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Latest,
            kind: SourceCodeKind.Regular);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            parseOptions,
            path: "Program.cs");

        var compilationOptions = new CSharpCompilationOptions(
            outputKind: OutputKind.ConsoleApplication,
            nullableContextOptions: NullableContextOptions.Enable,
            optimizationLevel: OptimizationLevel.Release,
            warningLevel: 4,
            allowUnsafe: false,
            deterministic: true);

        var compilation = CSharpCompilation.Create(
            AssemblyName,
            syntaxTrees: [syntaxTree],
            references: References.Value,
            options: compilationOptions);

        var diagnostics = compilation
            .GetDiagnostics()
            .Where(d => d.Severity != DiagnosticSeverity.Hidden)
            .Select(ToDiagnosticInfo)
            .ToArray();

        var hasErrors = diagnostics.Any(d => d.Severity == "Error");

        if (hasErrors)
        {
            return new CompileResult(false, diagnostics, null);
        }

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        if (!emitResult.Success)
        {
            var emitDiagnostics = emitResult.Diagnostics
                .Where(d => d.Severity != DiagnosticSeverity.Hidden)
                .Select(ToDiagnosticInfo)
                .ToArray();

            return new CompileResult(false, emitDiagnostics, null);
        }

        return new CompileResult(true, diagnostics, peStream.ToArray());
    }

    private static DiagnosticInfo ToDiagnosticInfo(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();

        return new DiagnosticInfo(
            Id: diagnostic.Id,
            Severity: diagnostic.Severity.ToString(),
            Message: diagnostic.GetMessage(),
            FilePath: span.Path,
            StartLine: span.StartLinePosition.Line + 1,
            StartColumn: span.StartLinePosition.Character + 1,
            EndLine: span.EndLinePosition.Line + 1,
            EndColumn: span.EndLinePosition.Character + 1);
    }

    private static MetadataReference[] BuildReferences()
    {
        // FR-043: a documented, curated set of framework references. In the single-file
        // browser-wasm publish, framework assemblies are available via the trusted platform
        // assemblies list exposed by the runtime.
        var trustedAssembliesPaths = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator) ?? [];

        var requiredAssemblyNames = new[]
        {
            "System.Private.CoreLib",
            "System.Runtime",
            "System.Collections",
            "System.Collections.Generic",
            "System.Linq",
            "System.Numerics",
            "System.Numerics.Vectors",
            "System.Text",
            "System.Text.Json",
            "System.Text.RegularExpressions",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Console",
            "System.Runtime.Extensions",
            "System.Runtime.Numerics",
            "System.ObjectModel",
            "System.Linq.Expressions",
            "netstandard",
        };

        var references = new List<MetadataReference>();

        foreach (var name in requiredAssemblyNames)
        {
            var path = trustedAssembliesPaths.FirstOrDefault(p =>
                string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase));

            if (path is not null && File.Exists(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        if (references.Count == 0)
        {
            // Fallback for hosts that do not populate TRUSTED_PLATFORM_ASSEMBLIES (e.g. some
            // browser-wasm configurations): reference the currently loaded core assemblies.
            var loadedAssemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Console).Assembly,
                typeof(System.Linq.Enumerable).Assembly,
                typeof(System.Text.Json.JsonSerializer).Assembly,
                typeof(System.Collections.Generic.List<>).Assembly,
            }.Distinct();

            references.AddRange(loadedAssemblies.Select(a => MetadataReference.CreateFromFile(a.Location)));
        }

        return references.ToArray();
    }
}

internal sealed record CompileResult(bool Success, DiagnosticInfo[] Diagnostics, byte[]? AssemblyBytes);

internal sealed record DiagnosticInfo(
    string Id,
    string Severity,
    string Message,
    string? FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);
