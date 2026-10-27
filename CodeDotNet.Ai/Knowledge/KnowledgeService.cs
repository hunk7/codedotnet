using System.Security.Cryptography;
using System.Text;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;

namespace CodeDotNet.Ai.Knowledge;

public sealed record IngestResult(string DocumentId, int Chunks, bool Embedded, string? Note);

public sealed record RetrievedChunk(ChunkRecord Chunk, string DocumentTitle, string Source);

public sealed record Retrieval(IReadOnlyList<RetrievedChunk> Chunks, bool UsedVectors, string? Note);

/// <summary>
/// Knowledge-base ingestion and hybrid retrieval (lexical FTS5 + vector, fused with RRF).
/// Documents arrive as text in the request body; the service never reads paths from the client,
/// so there is no path-traversal surface. Vector search degrades to lexical-only when embeddings
/// are unavailable, and says so.
/// </summary>
public sealed class KnowledgeService(
    IChunkRepository chunks,
    IVectorStore vectors,
    IEmbeddingProvider embeddings,
    IResourceGovernor governor,
    ResourceProfile profile,
    AiOptions options)
{
    public const int MaxDocumentChars = 200_000;
    public const int MaxTitleChars = 200;
    public const int MaxSourceChars = 500;

    public static string? Validate(string? title, string? source, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "'text' is required.";
        }

        if (text.Length > MaxDocumentChars)
        {
            return $"'text' exceeds {MaxDocumentChars} characters.";
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleChars)
        {
            return $"'title' is required and must be at most {MaxTitleChars} characters.";
        }

        return source is { Length: > MaxSourceChars } ? $"'source' exceeds {MaxSourceChars} characters." : null;
    }

    public static string DocumentId(string source) =>
        "d-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)))[..12].ToLowerInvariant();

    public async Task<IngestResult> IngestAsync(string title, string? source, string text, CancellationToken ct)
    {
        var src = string.IsNullOrWhiteSpace(source) ? title : source.Trim();
        var id = DocumentId(src);
        var pieces = MarkdownChunker.Chunk(text);
        var records = pieces.Select((p, i) => new ChunkRecord($"{id}-{i}", id, i, p.Heading, p.Text)).ToList();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        if (await chunks.GetDocumentAsync(id, ct).ConfigureAwait(false) is not null)
        {
            var old = await chunks.DeleteDocumentAsync(id, ct).ConfigureAwait(false);
            await vectors.DeleteAsync(old, ct).ConfigureAwait(false);
        }

        await chunks.UpsertDocumentAsync(new DocumentRecord(id, title.Trim(), src, hash), records, ct).ConfigureAwait(false);

        var (embedded, note) = await TryEmbedAsync(records, ct).ConfigureAwait(false);
        return new IngestResult(id, records.Count, embedded, note);
    }

    public async Task<bool> DeleteAsync(string documentId, CancellationToken ct)
    {
        if (await chunks.GetDocumentAsync(documentId, ct).ConfigureAwait(false) is null)
        {
            return false;
        }

        var ids = await chunks.DeleteDocumentAsync(documentId, ct).ConfigureAwait(false);
        await vectors.DeleteAsync(ids, ct).ConfigureAwait(false);
        return true;
    }

    public Task<IReadOnlyList<DocumentRecord>> ListAsync(CancellationToken ct) => chunks.ListDocumentsAsync(ct);

    public Task<int> CountChunksAsync(CancellationToken ct) => chunks.CountChunksAsync(ct);

    public async Task<Retrieval> RetrieveAsync(string query, CancellationToken ct)
    {
        var lexical = await chunks.SearchLexicalAsync(query, profile.LexicalCandidates, ct).ConfigureAwait(false);

        IReadOnlyList<VectorHit> vector = [];
        string? note = null;
        try
        {
            if (await vectors.GetManifestAsync(ct).ConfigureAwait(false) is not null)
            {
                float[] q;
                await using (await governor.AcquireAsync(WorkKind.Embedding, ct).ConfigureAwait(false))
                {
                    q = (await embeddings.EmbedAsync(profile.EmbeddingModel, [query], profile.KeepAlive, options.ResolveNumGpu(), ct).ConfigureAwait(false))[0];
                }

                vector = await vectors.SearchAsync(q, profile.VectorCandidates, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            note = "Vector search unavailable; used lexical results only (" + ex.Message + ").";
        }

        // A chunk must have lexical support or clear a minimum vector similarity, otherwise nearest-neighbour
        // search would return unrelated text for any query and "insufficient evidence" could never trigger.
        var lexicalIds = lexical.Select(h => h.Chunk.Id).ToHashSet();
        var vectorIds = vector.Where(v => v.Score >= options.MinVectorScore).Select(v => v.ChunkId).ToList();
        var fused = RankFusion.Fuse([lexical.Select(h => h.Chunk.Id).ToList(), vectorIds]);
        var top = fused.Where(f => lexicalIds.Contains(f.Id) || vectorIds.Contains(f.Id)).Take(profile.FinalChunks).Select(f => f.Id).ToList();
        var records = await chunks.GetChunksAsync(top, ct).ConfigureAwait(false);

        var docs = new Dictionary<string, DocumentRecord?>();
        var result = new List<RetrievedChunk>();
        foreach (var r in records)
        {
            if (!docs.TryGetValue(r.DocumentId, out var d))
            {
                docs[r.DocumentId] = d = await chunks.GetDocumentAsync(r.DocumentId, ct).ConfigureAwait(false);
            }

            result.Add(new RetrievedChunk(r, d?.Title ?? "(unknown)", d?.Source ?? ""));
        }

        return new Retrieval(result, vector.Count > 0, note);
    }

    private async Task<(bool Embedded, string? Note)> TryEmbedAsync(IReadOnlyList<ChunkRecord> records, CancellationToken ct)
    {
        if (records.Count == 0)
        {
            return (false, null);
        }

        try
        {
            var entries = new List<VectorEntry>();
            var batch = Math.Max(1, profile.EmbeddingBatchSize);
            for (var i = 0; i < records.Count; i += batch)
            {
                var slice = records.Skip(i).Take(batch).ToList();
                IReadOnlyList<float[]> vecs;
                await using (await governor.AcquireAsync(WorkKind.Embedding, ct).ConfigureAwait(false))
                {
                    vecs = await embeddings.EmbedAsync(profile.EmbeddingModel, slice.Select(c => c.Heading.Length > 0 ? c.Heading + "\n" + c.Text : c.Text).ToList(),
                        profile.KeepAlive, options.ResolveNumGpu(), ct).ConfigureAwait(false);
                }

                if (vecs.Count != slice.Count)
                {
                    throw new InvalidOperationException("Embedding provider returned a different number of vectors than inputs.");
                }

                entries.AddRange(slice.Select((c, j) => new VectorEntry(c.Id, vecs[j])));
            }

            await vectors.InitializeAsync(new IndexManifest(profile.EmbeddingModel, null, entries[0].Embedding.Length, 1), ct).ConfigureAwait(false);
            await vectors.UpsertAsync(entries, ct).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            return (false, "Stored for lexical search only; embedding failed: " + ex.Message);
        }
    }
}
