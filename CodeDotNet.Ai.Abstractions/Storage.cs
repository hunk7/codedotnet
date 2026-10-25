using CodeDotNet.Contracts;

namespace CodeDotNet.Ai.Abstractions;

public sealed record DocumentRecord(string Id, string Title, string Source, string ContentHash);

public sealed record ChunkRecord(string Id, string DocumentId, int Ordinal, string Heading, string Text);

public sealed record ChunkHit(ChunkRecord Chunk, double Score);

public sealed record VectorEntry(string ChunkId, float[] Embedding);

public sealed record VectorHit(string ChunkId, double Score);

/// <summary>Describes the embedding model an index was built with. Mixed dimensions are forbidden.</summary>
public sealed record IndexManifest(string EmbeddingModel, string? EmbeddingDigest, int Dimension, int Version);

/// <summary>Documents, chunks and lexical (full-text) search.</summary>
public interface IChunkRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertDocumentAsync(DocumentRecord document, IReadOnlyList<ChunkRecord> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChunkHit>> SearchLexicalAsync(string query, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChunkRecord>> GetChunksAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default);

    Task<DocumentRecord?> GetDocumentAsync(string documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentRecord>> ListDocumentsAsync(CancellationToken cancellationToken = default);

    Task<int> CountChunksAsync(CancellationToken cancellationToken = default);
}

/// <summary>Vector storage behind a swappable adapter (SQLite default, Qdrant optional).</summary>
public interface IVectorStore
{
    Task<IndexManifest?> GetManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the index or verifies the manifest. Throws on a dimension mismatch.</summary>
    Task InitializeAsync(IndexManifest manifest, CancellationToken cancellationToken = default);

    Task UpsertAsync(IReadOnlyList<VectorEntry> entries, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorHit>> SearchAsync(float[] query, int limit, CancellationToken cancellationToken = default);

    Task DeleteAsync(IReadOnlyList<string> chunkIds, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>Runs C# in the existing sandbox (MCP <c>compile_and_run</c>). Never runs code in-process.</summary>
public interface ISandboxClient
{
    Task<CompileAndRunResponse> CompileAndRunAsync(string source, string stdin, int timeoutMs, CancellationToken cancellationToken = default);
}
