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

    /// <summary><c>sqlite</c> (default, no extra process) or <c>qdrant</c> (optional adapter, ADR-011).</summary>
    public string VectorStore { get; set; } = "sqlite";

    /// <summary>Qdrant REST endpoint. Must be loopback; Qdrant is never exposed to the LAN by this service.</summary>
    public string QdrantUrl { get; set; } = "http://127.0.0.1:6333";

    public string QdrantCollection { get; set; } = "codedotnet_chunks";

    public string? QdrantApiKey { get; set; }

    public bool UseQdrant => string.Equals(VectorStore, "qdrant", StringComparison.OrdinalIgnoreCase);

    /// <summary>Throws <see cref="InvalidOperationException"/> for an unknown store, a non-loopback Qdrant URL or a bad collection name.</summary>
    public void ValidateVectorStore()
    {
        if (!UseQdrant)
        {
            if (!string.Equals(VectorStore, "sqlite", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Ai:VectorStore must be 'sqlite' or 'qdrant', not '{VectorStore}'.");
            }

            return;
        }

        if (!Uri.TryCreate(QdrantUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Ai:QdrantUrl must be an absolute http(s) URL.");
        }

        if (!uri.IsLoopback)
        {
            throw new InvalidOperationException("Ai:QdrantUrl must point to a loopback address (127.0.0.1, ::1 or localhost).");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(QdrantCollection ?? string.Empty, "^[A-Za-z0-9_-]{1,64}$"))
        {
            throw new InvalidOperationException("Ai:QdrantCollection must be 1-64 characters of letters, digits, '_' or '-'.");
        }
    }

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
