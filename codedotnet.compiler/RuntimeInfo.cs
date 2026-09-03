using System.Runtime.InteropServices.JavaScript;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeDotNet.Compiler;

/// <summary>
/// Exposes runtime and diagnostic information to the React host running on the main thread,
/// via JavaScript interop calls made from inside the dedicated Web Worker.
/// </summary>
[SupportedOSPlatform("browser")]
public partial class RuntimeInfo
{
    [JSExport]
    internal static string GetRuntimeInformation()
    {
        var info = new RuntimeInformationPayload(
            FrameworkDescription: RuntimeInformation.FrameworkDescription,
            EnvironmentVersion: Environment.Version.ToString(),
            OSArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeIdentifier: RuntimeInformation.RuntimeIdentifier);

        return JsonSerializer.Serialize(info, RuntimeInfoJsonContext.Default.RuntimeInformationPayload);
    }
}

internal sealed record RuntimeInformationPayload(
    string FrameworkDescription,
    string EnvironmentVersion,
    string OSArchitecture,
    string ProcessArchitecture,
    string RuntimeIdentifier);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RuntimeInformationPayload))]
internal sealed partial class RuntimeInfoJsonContext : JsonSerializerContext
{
}
