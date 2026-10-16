namespace CodeDotNet.Contracts;

/// <summary>
/// Result of executing a compiled assembly's entry point via <c>CodeDotNet.Engine</c>.
/// </summary>
public sealed record ExecutionResult(
    string Output,
    bool OutputTruncated,
    int ExitCode,
    string? ExceptionType,
    string? ExceptionMessage,
    string? ExceptionStackTrace);
