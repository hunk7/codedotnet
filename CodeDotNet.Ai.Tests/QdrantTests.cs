using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;
using CodeDotNet.Ai.Knowledge;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeDotNet.Ai.Tests;

/// <summary>In-memory stand-in for the subset of the Qdrant REST API the adapter uses. It is NOT Qdrant; see the gated live test.</summary>
internal sealed class FakeQdrantHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (int Size, Dictionary<string, (float[] Vector, JsonObject Payload)> Points)> collections = new();

    public int Requests { get; private set; }

    public bool Down { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        if (Down)
        {
            throw new HttpRequestException("connection refused");
        }

        var segs = request.RequestUri!.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));

        if (segs.Length == 0)
        {
            return Json(HttpStatusCode.OK, new JsonObject { ["title"] = "qdrant - vector search engine", ["version"] = "1.fake" });
        }

        var name = segs[1];
        if (segs.Length == 2)
        {
            if (request.Method == HttpMethod.Get)
            {
                return collections.TryGetValue(name, out var c)
                    ? Json(HttpStatusCode.OK, new JsonObject { ["result"] = new JsonObject { ["config"] = new JsonObject { ["params"] = new JsonObject { ["vectors"] = new JsonObject { ["size"] = c.Size, ["distance"] = "Cosine" } } } } })
                    : Json(HttpStatusCode.NotFound, new JsonObject { ["status"] = new JsonObject { ["error"] = "Not found" } });
            }

            var size = body!["vectors"]!["size"]!.GetValue<int>();
            collections[name] = (size, new());
            return Json(HttpStatusCode.OK, new JsonObject { ["result"] = true });
        }

        if (!collections.TryGetValue(name, out var col))
        {
            return Json(HttpStatusCode.NotFound, new JsonObject { ["status"] = new JsonObject { ["error"] = "Not found" } });
        }

        var op = segs.Length == 3 ? (request.Method == HttpMethod.Put ? "upsert" : "retrieve") : segs[3];
        switch (op)
        {
            case "upsert":
                foreach (var p in body!["points"]!.AsArray())
                {
                    var vec = p!["vector"]!.AsArray().Select(x => x!.GetValue<float>()).ToArray();
                    if (vec.Length != col.Size)
                    {
                        return Json(HttpStatusCode.BadRequest, new JsonObject { ["status"] = new JsonObject { ["error"] = "Wrong input: Vector dimension error" } });
                    }

                    col.Points[p["id"]!.GetValue<string>()] = (vec, (JsonObject)p["payload"]!.DeepClone());
                }

                return Json(HttpStatusCode.OK, new JsonObject { ["result"] = new JsonObject { ["status"] = "completed" } });

            case "retrieve":
                var found = new JsonArray();
                foreach (var id in body!["ids"]!.AsArray())
                {
                    if (col.Points.TryGetValue(id!.GetValue<string>(), out var pt))
                    {
                        found.Add(new JsonObject { ["id"] = id.GetValue<string>(), ["payload"] = pt.Payload.DeepClone() });
                    }
                }

                return Json(HttpStatusCode.OK, new JsonObject { ["result"] = found });

            case "delete":
                foreach (var id in body!["points"]!.AsArray())
                {
                    col.Points.Remove(id!.GetValue<string>());
                }

                return Json(HttpStatusCode.OK, new JsonObject { ["result"] = new JsonObject { ["status"] = "completed" } });

            case "count":
                return Json(HttpStatusCode.OK, new JsonObject { ["result"] = new JsonObject { ["count"] = Candidates(col.Points, body).Count() } });

            case "search":
                var q = body!["vector"]!.AsArray().Select(x => x!.GetValue<float>()).ToArray();
                if (q.Length != col.Size)
                {
                    return Json(HttpStatusCode.BadRequest, new JsonObject { ["status"] = new JsonObject { ["error"] = "Wrong input: Vector dimension error" } });
                }

                var limit = body["limit"]!.GetValue<int>();
                var ranked = Candidates(col.Points, body)
                    .Select(kv => (kv.Key, kv.Value.Payload, Score: Cosine(q, kv.Value.Vector)))
                    .OrderByDescending(x => x.Score)
                    .Take(limit);
                var arr = new JsonArray();
                foreach (var r in ranked)
                {
                    arr.Add(new JsonObject { ["id"] = r.Key, ["score"] = r.Score, ["payload"] = r.Payload.DeepClone() });
                }

                return Json(HttpStatusCode.OK, new JsonObject { ["result"] = arr });
        }

        return Json(HttpStatusCode.NotFound, new JsonObject());
    }

    private static IEnumerable<KeyValuePair<string, (float[] Vector, JsonObject Payload)>> Candidates(
        Dictionary<string, (float[] Vector, JsonObject Payload)> points, JsonNode? body)
    {
        var excludeManifest = body?["filter"]?["must_not"] is not null;
        return points.Where(kv => !(excludeManifest && kv.Value.Payload["kind"]?.GetValue<string>() == "manifest"));
    }

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }

        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
}

/// <summary>Runs one contract suite against every <see cref="IVectorStore"/> implementation to prove adapter parity.</summary>
public class VectorStoreParityTests
{
    public static TheoryData<string> Backends => new() { "sqlite", "qdrant-fake" };

    private static (IVectorStore Store, IDisposable Cleanup) Create(string backend)
    {
        if (backend == "sqlite")
        {
            var kb = new KbFixture();
            return (kb.Vectors, kb);
        }

        var options = new AiOptions { VectorStore = "qdrant", QdrantCollection = "t_" + Guid.NewGuid().ToString("N") };
        var http = new HttpClient(new FakeQdrantHandler()) { BaseAddress = new Uri("http://127.0.0.1:6333/") };
        return (new QdrantVectorStore(http, options), http);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task RoundTrip_ReturnsNearestFirst_WithMatchingScores(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        await store.InitializeAsync(new("m", null, 3, 1));
        await store.UpsertAsync([new("a", [1, 0, 0]), new("b", [0, 1, 0]), new("c", [0.9f, 0.1f, 0])]);

        var hits = await store.SearchAsync([1, 0, 0], 2);
        Assert.Equal(["a", "c"], hits.Select(h => h.ChunkId));
        Assert.Equal(1.0, hits[0].Score, 4);
        Assert.Equal(0.9939, hits[1].Score, 3);
        Assert.Equal(3, await store.CountAsync());
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Upsert_SameId_Replaces_AndDeleteRemoves(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        await store.InitializeAsync(new("m", null, 2, 1));
        await store.UpsertAsync([new("a", [1, 0])]);
        await store.UpsertAsync([new("a", [0, 1])]);
        Assert.Equal(1, await store.CountAsync());

        var hits = await store.SearchAsync([0, 1], 5);
        Assert.Equal("a", Assert.Single(hits).ChunkId);

        await store.DeleteAsync(["a", "missing"]);
        Assert.Equal(0, await store.CountAsync());
        Assert.Empty(await store.SearchAsync([0, 1], 5));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task DimensionMismatch_IsRejected(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        await store.InitializeAsync(new("m", null, 3, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync([new("a", [1, 0])]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SearchAsync([1, 0], 1));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task ChangingModelOrDimension_RequiresRebuild(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        await store.InitializeAsync(new("m", null, 3, 1));
        await store.InitializeAsync(new("m", null, 3, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializeAsync(new("m", null, 4, 1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.InitializeAsync(new("other", null, 3, 1)));
        var manifest = await store.GetManifestAsync();
        Assert.Equal(new IndexManifest("m", null, 3, 1), manifest);
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Uninitialized_SearchReturnsEmpty_AndManifestIsNull(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        Assert.Null(await store.GetManifestAsync());
        Assert.Empty(await store.SearchAsync([1, 0, 0], 3));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync([new("a", [1, 0, 0])]));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task ZeroVectorAndNonPositiveLimit_ReturnEmpty(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        await store.InitializeAsync(new("m", null, 2, 1));
        await store.UpsertAsync([new("a", [1, 0])]);
        Assert.Empty(await store.SearchAsync([0, 0], 3));
        Assert.Empty(await store.SearchAsync([1, 0], 0));
    }

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Count_IgnoresManifest_AndEmptyDeleteIsNoOp(string backend)
    {
        var (store, cleanup) = Create(backend);
        using var _ = cleanup;
        await store.InitializeAsync(new("m", null, 2, 1));
        Assert.Equal(0, await store.CountAsync());
        await store.DeleteAsync([]);
        Assert.Empty(await store.SearchAsync([1, 0], 5));
    }
}

public class QdrantVectorStoreTests
{
    private static (QdrantVectorStore Store, FakeQdrantHandler Handler) Make(string collection = "kb")
    {
        var handler = new FakeQdrantHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:6333/") };
        return (new QdrantVectorStore(http, new AiOptions { VectorStore = "qdrant", QdrantCollection = collection }), handler);
    }

    [Fact]
    public void PointId_IsDeterministic_AndDistinctPerChunk()
    {
        Assert.Equal(QdrantVectorStore.PointId("doc-1"), QdrantVectorStore.PointId("doc-1"));
        Assert.NotEqual(QdrantVectorStore.PointId("doc-1"), QdrantVectorStore.PointId("doc-2"));
    }

    [Fact]
    public async Task Search_NeverReturnsTheManifestPoint()
    {
        var (store, _) = Make();
        await store.InitializeAsync(new("m", null, 3, 1));
        Assert.Empty(await store.SearchAsync([1, 0, 0], 10));
    }

    [Fact]
    public async Task NonFiniteEmbedding_IsRejected_BeforeAnyWrite()
    {
        var (store, handler) = Make();
        await store.InitializeAsync(new("m", null, 2, 1));
        var before = handler.Requests;
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync([new("a", [float.NaN, 0])]));
        Assert.Equal(before, handler.Requests);
    }

    [Fact]
    public async Task Upsert_IsBatched_ForLargeInputs()
    {
        var (store, handler) = Make();
        await store.InitializeAsync(new("m", null, 2, 1));
        var before = handler.Requests;
        await store.UpsertAsync(Enumerable.Range(0, 130).Select(i => new VectorEntry("c" + i, [1, i])).ToList());
        Assert.Equal(3, handler.Requests - before);
        Assert.Equal(130, await store.CountAsync());
    }

    [Fact]
    public async Task ExistingCollection_WithDifferentSize_IsRefused()
    {
        var handler = new FakeQdrantHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:6333/") };
        var opts = new AiOptions { VectorStore = "qdrant", QdrantCollection = "shared" };
        await new QdrantVectorStore(http, opts).InitializeAsync(new("m", null, 3, 1));

        // A fresh adapter instance (no cache) against a collection whose manifest is gone must not silently reuse it.
        var other = new QdrantVectorStore(http, opts);
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.InitializeAsync(new("m", null, 5, 1)));
    }

    [Fact]
    public async Task Unreachable_Qdrant_SurfacesAsHttpRequestException()
    {
        var (store, handler) = Make();
        await store.InitializeAsync(new("m", null, 2, 1));
        handler.Down = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => store.SearchAsync([1, 0], 3));
        await Assert.ThrowsAsync<HttpRequestException>(() => store.PingAsync());
    }

    [Fact]
    public async Task Recovers_AfterOutage()
    {
        var (store, handler) = Make();
        await store.InitializeAsync(new("m", null, 2, 1));
        await store.UpsertAsync([new("a", [1, 0])]);
        handler.Down = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => store.SearchAsync([1, 0], 3));
        handler.Down = false;
        Assert.Equal("a", Assert.Single(await store.SearchAsync([1, 0], 3)).ChunkId);
    }

    [Fact]
    public async Task Ping_ReturnsVersion()
    {
        var (store, _) = Make();
        Assert.Equal("1.fake", await store.PingAsync());
    }
}

public class VectorStoreOptionsTests
{
    [Fact]
    public void Default_IsSqlite_AndValid()
    {
        var o = new AiOptions();
        Assert.False(o.UseQdrant);
        o.ValidateVectorStore();
    }

    [Theory]
    [InlineData("http://127.0.0.1:6333")]
    [InlineData("http://localhost:6333")]
    [InlineData("http://[::1]:6333")]
    public void Qdrant_LoopbackUrls_AreAccepted(string url) =>
        new AiOptions { VectorStore = "qdrant", QdrantUrl = url }.ValidateVectorStore();

    [Theory]
    [InlineData("http://192.168.1.10:6333")]
    [InlineData("http://example.com:6333")]
    [InlineData("http://0.0.0.0:6333")]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:6333")]
    public void Qdrant_NonLoopbackOrMalformedUrls_AreRejected(string url) =>
        Assert.Throws<InvalidOperationException>(() => new AiOptions { VectorStore = "qdrant", QdrantUrl = url }.ValidateVectorStore());

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("../x")]
    [InlineData("has space")]
    public void Qdrant_BadCollectionNames_AreRejected(string name) =>
        Assert.Throws<InvalidOperationException>(() => new AiOptions { VectorStore = "qdrant", QdrantCollection = name }.ValidateVectorStore());

    [Fact]
    public void UnknownStore_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => new AiOptions { VectorStore = "pinecone" }.ValidateVectorStore());

    [Fact]
    public void Selection_IsCaseInsensitive() => Assert.True(new AiOptions { VectorStore = "QDRANT" }.UseQdrant);

    [Fact]
    public void Host_DefaultsToSqliteVectorStore()
    {
        using var app = AiApp.Build(["--environment=Testing", "--Ai:DataDirectory=" + Path.Combine(Path.GetTempPath(), "cdn-vs-" + Guid.NewGuid().ToString("N"))]);
        Assert.IsType<SqliteVectorStore>(app.Services.GetRequiredService<IVectorStore>());
    }

    [Fact]
    public void Host_UsesQdrant_WhenConfigured()
    {
        using var app = AiApp.Build(["--environment=Testing", "--Ai:VectorStore=qdrant", "--Ai:DataDirectory=" + Path.Combine(Path.GetTempPath(), "cdn-vs-" + Guid.NewGuid().ToString("N"))]);
        Assert.IsType<QdrantVectorStore>(app.Services.GetRequiredService<IVectorStore>());
    }

    [Fact]
    public void Host_RefusesToStart_WithNonLoopbackQdrant() =>
        Assert.Throws<InvalidOperationException>(() => AiApp.Build(["--environment=Testing", "--Ai:VectorStore=qdrant", "--Ai:QdrantUrl=http://10.0.0.5:6333"]));
}

/// <summary>Runs the contract against a real Qdrant. Skipped unless CODEDOTNET_QDRANT_URL is set (e.g. http://127.0.0.1:6333).</summary>
public class QdrantLiveTests
{
    private static string? Url => Environment.GetEnvironmentVariable("CODEDOTNET_QDRANT_URL");

    [Fact]
    public async Task LiveQdrant_MatchesSqliteBehavior()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            return; // not run: no Qdrant available. Reported as "live: not run" in docs/ai/benchmarks.md, never as a pass.
        }

        var options = new AiOptions { VectorStore = "qdrant", QdrantUrl = Url, QdrantCollection = "cdn_test_" + Guid.NewGuid().ToString("N")[..12] };
        options.ValidateVectorStore();
        using var http = new HttpClient { BaseAddress = new Uri(Url.TrimEnd('/') + "/") };
        var store = new QdrantVectorStore(http, options);
        try
        {
            await store.InitializeAsync(new("m", null, 3, 1));
            await store.UpsertAsync([new("a", [1, 0, 0]), new("b", [0, 1, 0]), new("c", [0.9f, 0.1f, 0])]);
            var hits = await store.SearchAsync([1, 0, 0], 2);
            Assert.Equal(["a", "c"], hits.Select(h => h.ChunkId));
            Assert.Equal(0.9939, hits[1].Score, 3);
            Assert.Equal(3, await store.CountAsync());
            await store.DeleteAsync(["a"]);
            Assert.Equal(2, await store.CountAsync());
        }
        finally
        {
            using var del = new HttpRequestMessage(HttpMethod.Delete, $"collections/{options.QdrantCollection}");
            await http.SendAsync(del);
        }
    }
}
