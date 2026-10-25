using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Resources;

/// <summary>
/// Built-in provisional profiles from docs/ai/hardware-and-resource-profiles.md.
/// Model names are assumptions (A-04) and are overridable via configuration.
/// </summary>
public static class Profiles
{
    public const string LowResource = "low-resource";
    public const string Recommended = "recommended";

    public static readonly ResourceProfile Low = new(
        Name: LowResource,
        ChatModel: "qwen2.5-coder:1.5b",
        EmbeddingModel: "nomic-embed-text",
        NumCtx: 8192,
        NumPredict: 1024,
        KeepAlive: "2m",
        FinalChunks: 3,
        LexicalCandidates: 10,
        VectorCandidates: 10,
        AgentDefaultIterations: 3,
        AgentMaxIterations: 4,
        SandboxPoolSize: 1,
        EmbeddingBatchSize: 4,
        PauseEmbeddingDuringGeneration: true,
        LlmRerankerEnabled: false);

    public static readonly ResourceProfile Standard = new(
        Name: Recommended,
        ChatModel: "qwen2.5-coder:7b",
        EmbeddingModel: "nomic-embed-text",
        NumCtx: 8192,
        NumPredict: 2048,
        KeepAlive: "10m",
        FinalChunks: 5,
        LexicalCandidates: 20,
        VectorCandidates: 20,
        AgentDefaultIterations: 4,
        AgentMaxIterations: 6,
        SandboxPoolSize: 2,
        EmbeddingBatchSize: 8,
        PauseEmbeddingDuringGeneration: true,
        LlmRerankerEnabled: false);

    public static ResourceProfile? ByName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        LowResource => Low,
        Recommended => Standard,
        _ => null,
    };
}
