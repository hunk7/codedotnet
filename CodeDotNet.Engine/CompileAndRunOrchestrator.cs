using System.Diagnostics;
using CodeDotNet.Contracts;

namespace CodeDotNet.Engine;

/// <summary>
/// Combined compile-and-run orchestration shared by every host. Hosts (browser-wasm
/// JSExport adapter, MCP sandbox worker) call <see cref="CompileAndRun"/> and simply
/// serialize/transport the resulting <see cref="CompileAndRunResponse"/>.
/// </summary>
public static class CompileAndRunOrchestrator
{
    public static CompileAndRunResponse CompileAndRun(string source, string stdin)
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

            return new CompileAndRunResponse(
                Status: "BuildFailed",
                Diagnostics: [
                    new DiagnosticInfo("CDN000", "Error", $"Internal compiler error: {ex.Message}", "Program.cs", 1, 1, 1, 1),
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
        }

        compileStopwatch.Stop();

        var diagnostics = compileResult.Diagnostics;

        if (!compileResult.Success || compileResult.AssemblyBytes is null)
        {
            stopwatch.Stop();

            return new CompileAndRunResponse(
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

            return new CompileAndRunResponse(
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
        }

        executionStopwatch.Stop();
        stopwatch.Stop();

        var status = executionResult.ExceptionType is not null
            ? "ExecutionFailed"
            : diagnostics.Any(d => d.Severity == "Warning")
                ? "ExecutionCompletedWithWarnings"
                : "ExecutionCompleted";

        return new CompileAndRunResponse(
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
    }
}
