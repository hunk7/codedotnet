using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using CodeDotNet.Contracts;

namespace CodeDotNet.Compiler;

/// <summary>
/// Thin browser-wasm JS interop adapter exposing <c>CodeDotNet.Engine</c>'s runtime
/// information to the React host running on the main thread, via JavaScript interop calls
/// made from inside the dedicated Web Worker.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class RuntimeInfo
{
    [JSExport]
    internal static string GetRuntimeInformation()
    {
        var info = CodeDotNet.Engine.RuntimeInfo.GetRuntimeInformation();
        return JsonSerializer.Serialize(info, CodeDotNetJsonContext.Default.RuntimeInformationPayload);
    }
}
