namespace CodeDotNet.Ai.Abstractions;

public sealed record ChatMessage(string Role, string Content);

/// <summary>Every request carries explicit limits; nothing relies on runtime defaults.</summary>
public sealed record ModelOptions(
    string Model,
    int NumCtx,
    int NumPredict,
    string KeepAlive,
    int? NumGpu,
    double Temperature = 0.2);

public sealed record ChatRequest(IReadOnlyList<ChatMessage> Messages, ModelOptions Options);

public sealed record ChatToken(string Text, bool Done, string? DoneReason = null);

public sealed record ModelInfo(string Name, string? Digest, long SizeBytes);

public sealed record ProviderCapabilities(bool Reachable, string? Version, IReadOnlyList<ModelInfo> Models, string? Error = null);

public interface IChatProvider
{
    IAsyncEnumerable<ChatToken> StreamChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
}

public interface IEmbeddingProvider
{
    Task<IReadOnlyList<float[]>> EmbedAsync(
        string model,
        IReadOnlyList<string> inputs,
        string keepAlive,
        int? numGpu,
        CancellationToken cancellationToken = default);
}
