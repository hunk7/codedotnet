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
    /// <remarks>
    /// <see cref="LazyThreadSafetyMode.PublicationOnly"/> is used because the browser-wasm host
    /// does not support blocking waits on a lock's monitor ("Cannot wait on monitors on this
    /// runtime"), which is what the default <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>
    /// mode uses when the value is accessed concurrently. PublicationOnly still guarantees a
    /// single published result but never blocks a thread waiting on another; at worst the
    /// factory runs more than once, which is safe here since <see cref="BuildReferences"/> is
    /// pure and side-effect free. Unlike <see cref="LazyThreadSafetyMode.None"/>, it also remains
    /// safe under genuine multi-threaded access (e.g. parallel test execution) instead of
    /// throwing a reentrancy exception.
    /// </remarks>
    private static readonly Lazy<MetadataReference[]> References = new(BuildReferences, LazyThreadSafetyMode.PublicationOnly);

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
            deterministic: true,
            // Roslyn's default (true) parallelizes binding/diagnostics via the thread pool.
            // The single-threaded browser-wasm runtime cannot service that parallelism and
            // throws "Cannot wait on monitors on this runtime" the moment any real contention
            // occurs, so concurrent build must be disabled here.
            concurrentBuild: false);

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
        // FR-043: a documented, curated set of framework references.
        //
        // NOTE: on the browser-wasm host, assemblies are loaded from an in-memory bundle rather
        // than disk, so Assembly.Location is empty for every loaded assembly and
        // TRUSTED_PLATFORM_ASSEMBLIES is typically not populated at all. Building references from
        // file paths (via MetadataReference.CreateFromFile) is therefore unreliable in the
        // browser and was the root cause of the "Internal compiler error: Argument_EmptyString"
        // crash reported on the very first compile (MetadataReference.CreateFromFile("") throws
        // because Location resolves to an empty string).
        //
        // Instead, use the Basic.Reference.Assemblies package, which embeds pre-built reference
        // assembly bytes as resources and exposes them as ready-to-use MetadataReference
        // instances. This has no dependency on the file system or on which assemblies happen to
        // already be loaded into the current process, so it works reliably in browser-wasm.
        //
        // IMPORTANT: this must match the worker's own TargetFramework (net10.0). Using the Net90
        // reference set here caused every compiled program to bind to "System.Runtime,
        // Version=9.0.0.0", while the actual runtime hosting ExecutionHost.Run is .NET 10
        // (Version=10.0.0.0), so Assembly.Load/EntryPoint resolution failed with
        // FileNotFoundException at execution time even though compilation succeeded.
        return [.. Basic.Reference.Assemblies.Net100.References.All];
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
