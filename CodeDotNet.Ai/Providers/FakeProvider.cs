using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Providers;

/// <summary>
/// Deterministic in-memory provider for tests and for running the service without a model.
/// Embeddings are hashed bag-of-words vectors: lexically similar text is close, which is
/// enough to exercise retrieval plumbing. They are NOT semantic and are not a quality signal.
/// </summary>
public sealed class FakeProvider : IChatProvider, IEmbeddingProvider
{
    public const int Dimension = 64;

    private readonly Func<IReadOnlyList<ChatMessage>, string> _reply;

    private readonly string[] _models;

    public FakeProvider(Func<IReadOnlyList<ChatMessage>, string>? reply = null, params string[] models)
    {
        _models = models.Length > 0 ? models : ["fake-chat"];
        _reply = reply ?? (messages => "fake: " + (messages.LastOrDefault()?.Content ?? ""));
    }

    public List<ChatRequest> Requests { get; } = [];

    /// <summary>When set, EmbedAsync throws this exception (simulates an unavailable embedding model).</summary>
    public Exception? EmbedFailure { get; init; }

    public async IAsyncEnumerable<ChatToken> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        var text = _reply(request.Messages);
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new ChatToken(word + " ", false);
        }

        yield return new ChatToken("", true, "stop");
    }

    public Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProviderCapabilities(true, "fake", _models.Select(m => new ModelInfo(m, null, 0)).ToList()));

    public Task<IReadOnlyList<float[]>> EmbedAsync(
        string model,
        IReadOnlyList<string> inputs,
        string keepAlive,
        int? numGpu,
        CancellationToken cancellationToken = default) =>
        EmbedFailure is not null
            ? Task.FromException<IReadOnlyList<float[]>>(EmbedFailure)
            : Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(Embed).ToList());

    public static float[] Embed(string text)
    {
        var v = new float[Dimension];
        foreach (var token in Tokenize(text))
        {
            var h = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            v[h[0] % Dimension] += 1f;
        }

        var norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm > 0)
        {
            for (var i = 0; i < v.Length; i++)
            {
                v[i] /= norm;
            }
        }

        return v;
    }

    private static IEnumerable<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '\n', '\r', '\t', '.', ',', ';', '(', ')', '{', '}', '[', ']', '"', '\'', ':', '<', '>', '=', '+', '-', '*', '/'],
                StringSplitOptions.RemoveEmptyEntries);
}
