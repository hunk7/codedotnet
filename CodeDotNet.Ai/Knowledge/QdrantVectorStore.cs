using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;

namespace CodeDotNet.Ai.Knowledge;

/// <summary>
/// Optional vector store backed by a local Qdrant instance over its REST API (ADR-011). Cosine distance, so scores
/// match <see cref="SqliteVectorStore"/>. Chunk ids are mapped to deterministic UUIDs (Qdrant point ids must be UUIDs or
/// integers) and the original id is kept in the payload. The index manifest lives in one reserved point that is
/// excluded from search and count. Transport failures surface as <see cref="HttpRequestException"/>, which
/// <see cref="KnowledgeService"/> already treats as "fall back to lexical only".
/// </summary>
public sealed class QdrantVectorStore(HttpClient http, AiOptions options) : IVectorStore
{
    private const int UpsertBatchSize = 64;
    private static readonly Guid ManifestPointId = new("00000000-0000-0000-0000-00000000c0de");
    private static readonly JsonObject NotManifest = JsonNode.Parse("""{"must_not":[{"key":"kind","match":{"value":"manifest"}}]}""")!.AsObject();

    private IndexManifest? cached;

    private string Points => $"collections/{Uri.EscapeDataString(options.QdrantCollection)}/points";

    private string CollectionPath => $"collections/{Uri.EscapeDataString(options.QdrantCollection)}";

    /// <summary>Maps an application chunk id to a stable Qdrant point id.</summary>
    public static Guid PointId(string chunkId) => new(SHA256.HashData(Encoding.UTF8.GetBytes(chunkId)).AsSpan(0, 16));

    /// <summary>Returns the Qdrant version string; throws <see cref="HttpRequestException"/> when Qdrant is unreachable.</summary>
    public async Task<string> PingAsync(CancellationToken cancellationToken = default)
    {
        var root = await SendAsync(HttpMethod.Get, "/", null, cancellationToken).ConfigureAwait(false);
        return root?["version"]?.GetValue<string>() ?? "unknown";
    }

    public async Task<IndexManifest?> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        if (cached is not null)
        {
            return cached;
        }

        if (await SendAsync(HttpMethod.Get, CollectionPath, null, cancellationToken, allowNotFound: true).ConfigureAwait(false) is null)
        {
            return null;
        }

        var body = new JsonObject { ["ids"] = new JsonArray(ManifestPointId.ToString()), ["with_payload"] = true, ["with_vector"] = false };
        var reply = await SendAsync(HttpMethod.Post, Points, body, cancellationToken).ConfigureAwait(false);
        var node = reply?["result"]?[0]?["payload"]?["manifest"];
        return node is null ? null : cached = node.Deserialize<IndexManifest>();
    }

    public async Task InitializeAsync(IndexManifest manifest, CancellationToken cancellationToken = default)
    {
        var existing = await GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Dimension != manifest.Dimension || !string.Equals(existing.EmbeddingModel, manifest.EmbeddingModel, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Vector index was built with '{existing.EmbeddingModel}' ({existing.Dimension}d); requested '{manifest.EmbeddingModel}' ({manifest.Dimension}d). Rebuild the index.");
            }

            return;
        }

        var info = await SendAsync(HttpMethod.Get, CollectionPath, null, cancellationToken, allowNotFound: true).ConfigureAwait(false);
        if (info is null)
        {
            var create = new JsonObject { ["vectors"] = new JsonObject { ["size"] = manifest.Dimension, ["distance"] = "Cosine" } };
            await SendAsync(HttpMethod.Put, CollectionPath, create, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var size = info["result"]?["config"]?["params"]?["vectors"]?["size"]?.GetValue<int>();
            if (size != manifest.Dimension)
            {
                throw new InvalidOperationException(
                    $"Qdrant collection '{options.QdrantCollection}' exists with {(size?.ToString() ?? "unknown")} dimensions; requested {manifest.Dimension}. Use another collection or delete it.");
            }
        }

        var anchor = new float[manifest.Dimension];
        anchor[0] = 1;
        var point = new JsonObject
        {
            ["id"] = ManifestPointId.ToString(),
            ["vector"] = ToJson(anchor),
            ["payload"] = new JsonObject { ["kind"] = "manifest", ["manifest"] = JsonSerializer.SerializeToNode(manifest) },
        };
        await SendAsync(HttpMethod.Put, Points + "?wait=true", new JsonObject { ["points"] = new JsonArray(point) }, cancellationToken).ConfigureAwait(false);
        cached = manifest;
    }

    public async Task UpsertAsync(IReadOnlyList<VectorEntry> entries, CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Vector store is not initialized.");

        foreach (var e in entries)
        {
            if (e.Embedding.Length != manifest.Dimension)
            {
                throw new InvalidOperationException($"Embedding has {e.Embedding.Length} dimensions; index expects {manifest.Dimension}.");
            }

            if (e.Embedding.Any(x => !float.IsFinite(x)))
            {
                throw new InvalidOperationException("Embedding contains a non-finite value.");
            }
        }

        foreach (var batch in entries.Chunk(UpsertBatchSize))
        {
            var points = new JsonArray();
            foreach (var e in batch)
            {
                points.Add(new JsonObject
                {
                    ["id"] = PointId(e.ChunkId).ToString(),
                    ["vector"] = ToJson(e.Embedding),
                    ["payload"] = new JsonObject { ["chunkId"] = e.ChunkId },
                });
            }

            await SendAsync(HttpMethod.Put, Points + "?wait=true", new JsonObject { ["points"] = points }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(float[] query, int limit, CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (manifest is null || limit <= 0)
        {
            return [];
        }

        if (query.Length != manifest.Dimension)
        {
            throw new InvalidOperationException($"Query has {query.Length} dimensions; index expects {manifest.Dimension}.");
        }

        if (query.All(x => x == 0) || query.Any(x => !float.IsFinite(x)))
        {
            return [];
        }

        var body = new JsonObject
        {
            ["vector"] = ToJson(query),
            ["limit"] = limit,
            ["with_payload"] = new JsonArray("chunkId"),
            ["filter"] = NotManifest.DeepClone(),
        };
        var reply = await SendAsync(HttpMethod.Post, Points + "/search", body, cancellationToken).ConfigureAwait(false);

        var hits = new List<VectorHit>();
        foreach (var item in reply?["result"]?.AsArray() ?? [])
        {
            var id = item?["payload"]?["chunkId"]?.GetValue<string>();
            var score = item?["score"]?.GetValue<double>();
            if (id is not null && score is not null)
            {
                hits.Add(new VectorHit(id, score.Value));
            }
        }

        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.ChunkId, StringComparer.Ordinal).Take(limit).ToList();
    }

    public async Task DeleteAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        if (chunkIds.Count == 0)
        {
            return;
        }

        var ids = new JsonArray();
        foreach (var id in chunkIds)
        {
            ids.Add(PointId(id).ToString());
        }

        await SendAsync(HttpMethod.Post, Points + "/delete?wait=true", new JsonObject { ["points"] = ids }, cancellationToken, allowNotFound: true).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var body = new JsonObject { ["exact"] = true, ["filter"] = NotManifest.DeepClone() };
        var reply = await SendAsync(HttpMethod.Post, Points + "/count", body, cancellationToken, allowNotFound: true).ConfigureAwait(false);
        return reply?["result"]?["count"]?.GetValue<int>() ?? 0;
    }

    private static JsonArray ToJson(float[] v)
    {
        var a = new JsonArray();
        foreach (var x in v)
        {
            a.Add(JsonValue.Create(x));
        }

        return a;
    }

    private async Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken ct, bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
            {
                cached = null;
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                cached = null;
                var detail = text.Length > 300 ? text[..300] : text;
                throw new HttpRequestException($"Qdrant returned {(int)response.StatusCode}: {detail}", null, response.StatusCode);
            }

            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (HttpRequestException)
        {
            cached = null;
            throw;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            cached = null;
            throw new HttpRequestException("Qdrant request timed out.");
        }
    }
}
