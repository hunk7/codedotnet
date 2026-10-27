namespace CodeDotNet.Ai.Abstractions;

/// <summary>Controls whether the local model runtime may use the GPU.</summary>
public enum GpuMode
{
    /// <summary>Always CPU (<c>num_gpu = 0</c>).</summary>
    Off,

    /// <summary>CPU until a GPU validation result says otherwise.</summary>
    Auto,

    /// <summary>Let the runtime decide, including GPU offload.</summary>
    On,
}

/// <summary>Provisional resource configuration. Values are configuration, not benchmark results.</summary>
public sealed record ResourceProfile(
    string Name,
    string ChatModel,
    string EmbeddingModel,
    int NumCtx,
    int NumPredict,
    string KeepAlive,
    int FinalChunks,
    int LexicalCandidates,
    int VectorCandidates,
    int AgentDefaultIterations,
    int AgentMaxIterations,
    int SandboxPoolSize,
    int EmbeddingBatchSize,
    bool PauseEmbeddingDuringGeneration,
    bool LlmRerankerEnabled);

public sealed record HardwareInfo(
    string OsDescription,
    int LogicalCores,
    long TotalRamBytes,
    long FreeDiskBytes,
    string? GpuName);

public interface IHardwareProbe
{
    Task<HardwareInfo> ProbeAsync(CancellationToken cancellationToken = default);
}

public sealed record ProfileOverrides(string? ProfileName = null);

public interface IProfileResolver
{
    ResourceProfile Resolve(HardwareInfo hardware, ProfileOverrides overrides);
}

public enum WorkKind
{
    Generation,
    Embedding,
}

/// <summary>Serializes heavy local work so the machine is not oversubscribed.</summary>
public interface IResourceGovernor
{
    ValueTask<IAsyncDisposable> AcquireAsync(WorkKind kind, CancellationToken cancellationToken = default);
}
