using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeDotNet.Compiler;

/// <summary>
/// Exposes the combined compile-and-run operation to the React host over JS interop
/// (FR-040 through FR-074, protocol operation <c>CompileAndRun</c>).
/// </summary>
[SupportedOSPlatform("browser")]
public partial class CompileAndRunHost
{
    [JSExport]
    internal static string CompileAndRun(string source, string stdin)
    {
        var stopwatch = Stopwatch.StartNew();
        var compileStopwatch = Stopwatch.StartNew();

        CompileResult compileResult;

        try
        {
            compileResult = CompilerHost.Compile(source);
        }
        catch (Exception ex)
        {
            compileStopwatch.Stop();
            stopwatch.Stop();

            var failurePayload = new CompileAndRunResponse(
                Status: "BuildFailed",
                Diagnostics: [
                    new DiagnosticPayload("CDN000", "Error", $"Internal compiler error: {ex.Message}", "Program.cs", 1, 1, 1, 1),
                ],
                Output: string.Empty,
                OutputTruncated: false,
                ExitCode: null,
                ExceptionType: null,
                ExceptionMessage: null,
                ExceptionStackTrace: null,
                CompilationDurationMs: compileStopwatch.Elapsed.TotalMilliseconds,
                ExecutionDurationMs: 0,
                TotalDurationMs: stopwatch.Elapsed.TotalMilliseconds);

            return JsonSerializer.Serialize(failurePayload, CompileAndRunJsonContext.Default.CompileAndRunResponse);
        }

        compileStopwatch.Stop();

        var diagnostics = compileResult.Diagnostics
            .Select(d => new DiagnosticPayload(d.Id, d.Severity, d.Message, d.FilePath, d.StartLine, d.StartColumn, d.EndLine, d.EndColumn))
            .ToArray();

        if (!compileResult.Success || compileResult.AssemblyBytes is null)
        {
            stopwatch.Stop();

            var buildFailedPayload = new CompileAndRunResponse(
                Status: "BuildFailed",
                Diagnostics: diagnostics,
                Output: string.Empty,
                OutputTruncated: false,
                ExitCode: null,
                ExceptionType: null,
                ExceptionMessage: null,
                ExceptionStackTrace: null,
                CompilationDurationMs: compileStopwatch.Elapsed.TotalMilliseconds,
                ExecutionDurationMs: 0,
                TotalDurationMs: stopwatch.Elapsed.TotalMilliseconds);

            return JsonSerializer.Serialize(buildFailedPayload, CompileAndRunJsonContext.Default.CompileAndRunResponse);
        }

        var executionStopwatch = Stopwatch.StartNew();
        ExecutionResult executionResult;

        try
        {
            executionResult = ExecutionHost.Run(compileResult.AssemblyBytes, stdin);
        }
        catch (Exception ex)
        {
            executionStopwatch.Stop();
            stopwatch.Stop();

            var executionErrorPayload = new CompileAndRunResponse(
                Status: "ExecutionFailed",
                Diagnostics: diagnostics,
                Output: string.Empty,
                OutputTruncated: false,
                ExitCode: null,
                ExceptionType: ex.GetType().FullName ?? ex.GetType().Name,
                ExceptionMessage: ex.Message,
                ExceptionStackTrace: ex.StackTrace,
                CompilationDurationMs: compileStopwatch.Elapsed.TotalMilliseconds,
                ExecutionDurationMs: executionStopwatch.Elapsed.TotalMilliseconds,
                TotalDurationMs: stopwatch.Elapsed.TotalMilliseconds);

            return JsonSerializer.Serialize(executionErrorPayload, CompileAndRunJsonContext.Default.CompileAndRunResponse);
        }

        executionStopwatch.Stop();
        stopwatch.Stop();

        var status = executionResult.ExceptionType is not null
            ? "ExecutionFailed"
            : diagnostics.Any(d => d.Severity == "Warning")
                ? "ExecutionCompletedWithWarnings"
                : "ExecutionCompleted";

        var response = new CompileAndRunResponse(
            Status: status,
            Diagnostics: diagnostics,
            Output: executionResult.Output,
            OutputTruncated: executionResult.OutputTruncated,
            ExitCode: executionResult.ExitCode,
            ExceptionType: executionResult.ExceptionType,
            ExceptionMessage: executionResult.ExceptionMessage,
            ExceptionStackTrace: executionResult.ExceptionStackTrace,
            CompilationDurationMs: compileStopwatch.Elapsed.TotalMilliseconds,
            ExecutionDurationMs: executionStopwatch.Elapsed.TotalMilliseconds,
            TotalDurationMs: stopwatch.Elapsed.TotalMilliseconds);

        return JsonSerializer.Serialize(response, CompileAndRunJsonContext.Default.CompileAndRunResponse);
    }
}

internal sealed record DiagnosticPayload(
    string Id,
    string Severity,
    string Message,
    string? FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

internal sealed record CompileAndRunResponse(
    string Status,
    DiagnosticPayload[] Diagnostics,
    string Output,
    bool OutputTruncated,
    int? ExitCode,
    string? ExceptionType,
    string? ExceptionMessage,
    string? ExceptionStackTrace,
    double CompilationDurationMs,
    double ExecutionDurationMs,
    double TotalDurationMs);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CompileAndRunResponse))]
internal sealed partial class CompileAndRunJsonContext : JsonSerializerContext
{
}
