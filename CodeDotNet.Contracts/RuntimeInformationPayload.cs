namespace CodeDotNet.Contracts;

/// <summary>
/// Runtime/diagnostic information about the hosting .NET runtime, surfaced identically by
/// the browser worker and the MCP sandbox worker.
/// </summary>
public sealed record RuntimeInformationPayload(
    string FrameworkDescription,
    string EnvironmentVersion,
    string OSArchitecture,
    string ProcessArchitecture,
    string RuntimeIdentifier,
    string RoslynVersion,
    string LanguageVersion);
