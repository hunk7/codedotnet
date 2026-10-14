namespace CodeDotNet.Contracts;

/// <summary>
/// A single Roslyn diagnostic projected into a stable, JSON-serializable shape shared
/// across every host (browser worker, MCP sandbox worker, future hosts).
/// </summary>
public sealed record DiagnosticInfo(
    string Id,
    string Severity,
    string Message,
    string? FilePath,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);
