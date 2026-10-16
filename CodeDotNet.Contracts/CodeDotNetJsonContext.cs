using System.Text.Json.Serialization;

namespace CodeDotNet.Contracts;

/// <summary>
/// Source-generated JSON context for every contract type, shared by all hosts so that
/// serialization shape (camelCase) is defined exactly once.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CompileAndRunResponse))]
[JsonSerializable(typeof(RuntimeInformationPayload))]
public sealed partial class CodeDotNetJsonContext : JsonSerializerContext
{
}
