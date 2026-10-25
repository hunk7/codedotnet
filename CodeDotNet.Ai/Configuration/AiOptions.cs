using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 7901;

    public string OllamaBaseUrl { get; set; } = "http://127.0.0.1:11434";

    public string? Profile { get; set; }

    public string? ChatModel { get; set; }

    public string? EmbeddingModel { get; set; }

    public GpuMode GpuMode { get; set; } = GpuMode.Auto;

    public string DataDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeDotNet", "ai");

    /// <summary>Minimum cosine similarity for a vector-only retrieval hit. UNBENCHMARKED default; tune with BM-07/evaluation data.</summary>
    public double MinVectorScore { get; set; } = 0.5;

    public string SandboxUrl { get; set; } = "ws://127.0.0.1:7900";

    public string[] AllowedOrigins { get; set; } = [];

    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>Returns the profile with any explicit model overrides applied.</summary>
    public ResourceProfile ApplyOverrides(ResourceProfile profile) => profile with
    {
        ChatModel = string.IsNullOrWhiteSpace(ChatModel) ? profile.ChatModel : ChatModel,
        EmbeddingModel = string.IsNullOrWhiteSpace(EmbeddingModel) ? profile.EmbeddingModel : EmbeddingModel,
    };

    /// <summary>
    /// <c>off</c> forces CPU (0). <c>auto</c> stays on CPU until GPU validation exists (A-03).
    /// <c>on</c> leaves the decision to the runtime (null = omit the option).
    /// </summary>
    public int? ResolveNumGpu() => GpuMode == GpuMode.On ? null : 0;
}
