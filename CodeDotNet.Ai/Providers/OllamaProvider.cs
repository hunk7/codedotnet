using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeDotNet.Ai.Abstractions;

namespace CodeDotNet.Ai.Providers;

/// <summary>
/// Talks to a local Ollama server over its HTTP API. Every request sets num_ctx, num_predict,
/// keep_alive and (when decided) num_gpu explicitly. Endpoint shapes are from memory of the
/// Ollama API and must be verified against the installed version (A-03/A-04).
/// </summary>
public sealed class OllamaProvider(HttpClient http) : IChatProvider, IEmbeddingProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async IAsyncEnumerable<ChatToken> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model = request.Options.Model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            keep_alive = request.Options.KeepAlive,
            options = BuildOptions(request.Options),
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        using var response = await http
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var chunk = ParseChatLine(line);
            yield return chunk;
            if (chunk.Done)
            {
                yield break;
            }
        }
    }

    public async Task<ProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string? version = null;
            using (var v = await http.GetAsync("api/version", cancellationToken).ConfigureAwait(false))
            {
                if (v.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await v.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                    version = doc.RootElement.TryGetProperty("version", out var ver) ? ver.GetString() : null;
                }
            }

            var models = new List<ModelInfo>();
            using var tags = await http.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
            tags.EnsureSuccessStatusCode();
            using var tagDoc = JsonDocument.Parse(await tags.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (tagDoc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in arr.EnumerateArray())
                {
                    models.Add(new ModelInfo(
                        m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        m.TryGetProperty("digest", out var d) ? d.GetString() : null,
                        m.TryGetProperty("size", out var s) && s.TryGetInt64(out var size) ? size : 0));
                }
            }

            return new ProviderCapabilities(true, version, models);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new ProviderCapabilities(false, null, [], ex.Message);
        }
    }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(
        string model,
        IReadOnlyList<string> inputs,
        string keepAlive,
        int? numGpu,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model,
            input = inputs,
            keep_alive = keepAlive,
            options = numGpu is null ? null : new Dictionary<string, object> { ["num_gpu"] = numGpu.Value },
        };

        using var response = await http.PostAsJsonAsync("api/embed", body, Json, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return ParseEmbeddings(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    internal static Dictionary<string, object> BuildOptions(ModelOptions o)
    {
        var options = new Dictionary<string, object>
        {
            ["num_ctx"] = o.NumCtx,
            ["num_predict"] = o.NumPredict,
            ["temperature"] = o.Temperature,
        };
        if (o.NumGpu is { } gpu)
        {
            options["num_gpu"] = gpu;
        }

        return options;
    }

    internal static ChatToken ParseChatLine(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (root.TryGetProperty("error", out var err))
        {
            throw new InvalidOperationException($"Ollama error: {err.GetString()}");
        }

        var text = root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c)
            ? c.GetString() ?? ""
            : "";
        var done = root.TryGetProperty("done", out var d) && d.GetBoolean();
        var reason = root.TryGetProperty("done_reason", out var r) ? r.GetString() : null;
        return new ChatToken(text, done, reason);
    }

    internal static IReadOnlyList<float[]> ParseEmbeddings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("embeddings", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Ollama embed response has no 'embeddings' array.");
        }

        return arr.EnumerateArray()
            .Select(e => e.EnumerateArray().Select(x => x.GetSingle()).ToArray())
            .ToList();
    }
}
