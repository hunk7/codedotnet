namespace CodeDotNet.Contracts;

/// <summary>
/// Combined compile-and-run response shape returned to every host (browser worker JSON
/// payload, MCP sandbox tool result, etc). Property names use camelCase to match the
/// existing browser worker protocol (<c>codedotnet.web/src/workers/protocol.ts</c>).
/// </summary>
public sealed record CompileAndRunResponse(
    string Status,
    DiagnosticInfo[] Diagnostics,
    string Output,
    bool OutputTruncated,
    int? ExitCode,
    string? ExceptionType,
    string? ExceptionMessage,
    string? ExceptionStackTrace,
    double CompilationDurationMs,
    double ExecutionDurationMs,
    double TotalDurationMs);
