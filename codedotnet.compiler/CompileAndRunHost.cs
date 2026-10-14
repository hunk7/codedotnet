using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using CodeDotNet.Contracts;

namespace CodeDotNet.Compiler;

/// <summary>
/// Thin browser-wasm JS interop adapter exposing <c>CodeDotNet.Engine</c>'s combined
/// compile-and-run operation to the React host (FR-040 through FR-074, protocol operation
/// <c>CompileAndRun</c>). All actual compile/execute logic lives in
/// <see cref="CodeDotNet.Engine.CompileAndRunOrchestrator"/> so the browser and the MCP
/// sandbox host share identical behavior.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class CompileAndRunHost
{
    [JSExport]
    internal static string CompileAndRun(string source, string stdin)
    {
        var response = CodeDotNet.Engine.CompileAndRunOrchestrator.CompileAndRun(source, stdin);
        return JsonSerializer.Serialize(response, CodeDotNetJsonContext.Default.CompileAndRunResponse);
    }
}
