using System.Runtime.InteropServices;
using CodeDotNet.Contracts;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeDotNet.Engine;

/// <summary>
/// Produces runtime/diagnostic information about the hosting .NET runtime. Shared by every
/// host (browser worker, MCP sandbox worker) so the reported values are always consistent.
/// </summary>
public static class RuntimeInfo
{
    public static RuntimeInformationPayload GetRuntimeInformation()
    {
        return new RuntimeInformationPayload(
            FrameworkDescription: RuntimeInformation.FrameworkDescription,
            EnvironmentVersion: Environment.Version.ToString(),
            OSArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeIdentifier: RuntimeInformation.RuntimeIdentifier,
            RoslynVersion: typeof(CSharpCompilation).Assembly.GetName().Version?.ToString() ?? "unknown",
            LanguageVersion: LanguageVersion.Latest.ToDisplayString());
    }
}
