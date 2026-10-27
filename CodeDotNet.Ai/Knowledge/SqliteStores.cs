using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using CodeDotNet.Ai.Abstractions;
using CodeDotNet.Ai.Configuration;
using Microsoft.Data.Sqlite;

namespace CodeDotNet.Ai.Knowledge;

/// <summary>Owns the local SQLite file (under AiOptions.DataDirectory) and its schema.</summary>
public sealed class KbDatabase(AiOptions options)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(options.DataDirectory, "knowledge.db"),
    }.ToString();

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);
        var c = new SqliteConnection(ConnectionString);
        await c.OpenAsync(ct).ConfigureAwait(false);
        await using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        await pragma.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return c;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_ready)
        {
            return;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ready)
            {
                return;
            }

            Directory.CreateDirectory(options.DataDirectory);
            await using var c = new SqliteConnection(ConnectionString);
            await c.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                CREATE TABLE IF NOT EXISTS documents(id TEXT PRIMARY KEY, title TEXT NOT NULL, source TEXT NOT NULL, content_hash TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS chunks(id TEXT PRIMARY KEY, document_id TEXT NOT NULL REFERENCES documents(id) ON DELETE CASCADE, ordinal INTEGER NOT NULL, heading TEXT NOT NULL, text TEXT NOT NULL);
                CREATE VIRTUAL TABLE IF NOT EXISTS chunks_fts USING fts5(chunk_id UNINDEXED, heading, text, tokenize='unicode61');
                CREATE TABLE IF NOT EXISTS vectors(chunk_id TEXT PRIMARY KEY, embedding BLOB NOT NULL);
                CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
                """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}

public sealed partial class SqliteChunkRepository(KbDatabase db) : IChunkRepository
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        db.OpenAsync(cancellationToken).ContinueWith(t => t.Result.Dispose(), cancellationToken);

    public async Task UpsertDocumentAsync(DocumentRecord document, IReadOnlyList<ChunkRecord> chunks, CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await ExecAsync(c, tx, "DELETE FROM chunks_fts WHERE chunk_id IN (SELECT id FROM chunks WHERE document_id=$d)", ("$d", document.Id), cancellationToken);
        await ExecAsync(c, tx, "DELETE FROM chunks WHERE document_id=$d", ("$d", document.Id), cancellationToken);
        await ExecAsync(c, tx, "INSERT INTO documents(id,title,source,content_hash) VALUES($i,$t,$s,$h) ON CONFLICT(id) DO UPDATE SET title=$t, source=$s, content_hash=$h",
            cancellationToken, ("$i", document.Id), ("$t", document.Title), ("$s", document.Source), ("$h", document.ContentHash));

        foreach (var ch in chunks)
        {
            await ExecAsync(c, tx, "INSERT INTO chunks(id,document_id,ordinal,heading,text) VALUES($i,$d,$o,$h,$x)",
                cancellationToken, ("$i", ch.Id), ("$d", ch.DocumentId), ("$o", ch.Ordinal), ("$h", ch.Heading), ("$x", ch.Text));
            await ExecAsync(c, tx, "INSERT INTO chunks_fts(chunk_id,heading,text) VALUES($i,$h,$x)",
                cancellationToken, ("$i", ch.Id), ("$h", ch.Heading), ("$x", ch.Text));
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var ids = new List<string>();
        await using (var q = c.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "SELECT id FROM chunks WHERE document_id=$d";
            q.Parameters.AddWithValue("$d", documentId);
            await using var r = await q.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(r.GetString(0));
            }
        }

        await ExecAsync(c, tx, "DELETE FROM chunks_fts WHERE chunk_id IN (SELECT id FROM chunks WHERE document_id=$d)", ("$d", documentId), cancellationToken);
        await ExecAsync(c, tx, "DELETE FROM vectors WHERE chunk_id IN (SELECT id FROM chunks WHERE document_id=$d)", ("$d", documentId), cancellationToken);
        await ExecAsync(c, tx, "DELETE FROM chunks WHERE document_id=$d", ("$d", documentId), cancellationToken);
        await ExecAsync(c, tx, "DELETE FROM documents WHERE id=$d", ("$d", documentId), cancellationToken);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ids;
    }

    public async Task<IReadOnlyList<ChunkHit>> SearchLexicalAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var match = BuildMatch(query);
        if (match is null || limit <= 0)
        {
            return [];
        }

        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT c.id, c.document_id, c.ordinal, c.heading, c.text, bm25(chunks_fts)
            FROM chunks_fts JOIN chunks c ON c.id = chunks_fts.chunk_id
            WHERE chunks_fts MATCH $m ORDER BY bm25(chunks_fts) LIMIT $l
            """;
        cmd.Parameters.AddWithValue("$m", match);
        cmd.Parameters.AddWithValue("$l", limit);
        var hits = new List<ChunkHit>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            hits.Add(new ChunkHit(new ChunkRecord(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4)), -r.GetDouble(5)));
        }

        return hits;
    }

    public async Task<IReadOnlyList<ChunkRecord>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<string, ChunkRecord>();
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in chunkIds.Distinct())
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT id, document_id, ordinal, heading, text FROM chunks WHERE id=$i";
            cmd.Parameters.AddWithValue("$i", id);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found[id] = new ChunkRecord(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4));
            }
        }

        return chunkIds.Where(found.ContainsKey).Select(id => found[id]).ToList();
    }

    public async Task<DocumentRecord?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,source,content_hash FROM documents WHERE id=$i";
        cmd.Parameters.AddWithValue("$i", documentId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await r.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new DocumentRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3))
            : null;
    }

    public async Task<IReadOnlyList<DocumentRecord>> ListDocumentsAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,source,content_hash FROM documents ORDER BY title, id";
        var list = new List<DocumentRecord>();
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new DocumentRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)));
        }

        return list;
    }

    public async Task<int> CountChunksAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM chunks";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "is", "are", "was", "were", "to", "of", "in", "on", "for", "and", "or", "it", "this", "that",
        "what", "how", "why", "do", "does", "i", "my", "me", "can", "with", "as", "at", "be", "by", "from",
    };

    [GeneratedRegex(@"[\p{L}\p{N}_]+")]
    private static partial Regex WordRx();

    /// <summary>Builds a safe FTS5 query: every token is quoted, so user text can never inject FTS syntax.</summary>
    internal static string? BuildMatch(string query)
    {
        var tokens = WordRx().Matches(query).Select(m => m.Value).Where(t => t.Length > 1 && !Stop.Contains(t)).Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList();
        return tokens.Count == 0 ? null : string.Join(" OR ", tokens.Select(t => "\"" + t + "\""));
    }

    private static Task ExecAsync(SqliteConnection c, SqliteTransaction tx, string sql, (string, object) p, CancellationToken ct) =>
        ExecAsync(c, tx, sql, ct, p);

    private static async Task ExecAsync(SqliteConnection c, SqliteTransaction tx, string sql, CancellationToken ct, params (string Name, object Value)[] ps)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (n, v) in ps)
        {
            cmd.Parameters.AddWithValue(n, v);
        }

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Default vector store: float32 blobs in SQLite with exact (brute-force) cosine search. This is NOT sqlite-vec;
/// it is adequate for a personal knowledge base and sits behind IVectorStore so an ANN adapter can replace it (BM-08).
/// </summary>
public sealed class SqliteVectorStore(KbDatabase db) : IVectorStore
{
    public async Task<IndexManifest?> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key='vector_manifest'";
        var v = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return v is null ? null : System.Text.Json.JsonSerializer.Deserialize<IndexManifest>(v);
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

        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO meta(key,value) VALUES('vector_manifest',$v)";
        cmd.Parameters.AddWithValue("$v", System.Text.Json.JsonSerializer.Serialize(manifest));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertAsync(IReadOnlyList<VectorEntry> entries, CancellationToken cancellationToken = default)
    {
        var manifest = await GetManifestAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Vector store is not initialized.");
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await c.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var e in entries)
        {
            if (e.Embedding.Length != manifest.Dimension)
            {
                throw new InvalidOperationException($"Embedding has {e.Embedding.Length} dimensions; index expects {manifest.Dimension}.");
            }

            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO vectors(chunk_id,embedding) VALUES($i,$b) ON CONFLICT(chunk_id) DO UPDATE SET embedding=$b";
            cmd.Parameters.AddWithValue("$i", e.ChunkId);
            cmd.Parameters.AddWithValue("$b", MemoryMarshal.AsBytes(e.Embedding.AsSpan()).ToArray());
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
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

        var qNorm = Norm(query);
        if (qNorm == 0)
        {
            return [];
        }

        var hits = new List<VectorHit>();
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT chunk_id, embedding FROM vectors";
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var v = new float[manifest.Dimension];
            Buffer.BlockCopy((byte[])r["embedding"], 0, v, 0, Math.Min(manifest.Dimension * sizeof(float), ((byte[])r["embedding"]).Length));
            var n = Norm(v);
            if (n == 0)
            {
                continue;
            }

            double dot = 0;
            for (var i = 0; i < v.Length; i++)
            {
                dot += (double)v[i] * query[i];
            }

            hits.Add(new VectorHit(r.GetString(0), dot / (n * qNorm)));
        }

        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.ChunkId, StringComparer.Ordinal).Take(limit).ToList();
    }

    public async Task DeleteAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var id in chunkIds)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM vectors WHERE chunk_id=$i";
            cmd.Parameters.AddWithValue("$i", id);
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await db.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM vectors";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static double Norm(float[] v)
    {
        double s = 0;
        foreach (var x in v)
        {
            s += (double)x * x;
        }

        return Math.Sqrt(s);
    }
}
