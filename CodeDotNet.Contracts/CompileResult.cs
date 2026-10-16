namespace CodeDotNet.Contracts;

/// <summary>
/// Result of compiling a single-file C# program via <c>CodeDotNet.Engine</c>.
/// </summary>
public sealed record CompileResult(
    bool Success,
    DiagnosticInfo[] Diagnostics,
    byte[]? AssemblyBytes);
