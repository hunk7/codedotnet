# RAG Design

Status: **Implemented (Phases 3 and 7).** Defaults are unbenchmarked.

1. **Ingest** (`POST /kb/documents`): validate, chunk, store chunks in SQLite with an FTS5 index, embed through `IEmbeddingProvider`, write vectors to the configured `IVectorStore`.
2. **Index manifest**: the vector store records the embedding model and dimension. Changing either is refused until the index is rebuilt.
3. **Retrieve**: lexical (FTS5) and vector (exact cosine) results are fused. A vector-only hit must reach `Ai:MinVectorScore` (0.5, **unbenchmarked**).
4. **Fallback**: if embeddings or the vector store fail (including `HttpRequestException` from Qdrant), retrieval continues lexical-only.
5. **Answer** (`POST /ask`): the model gets numbered sources and must cite them. Without sufficient evidence the service says so. Citations stream as `citation` events.

Vector stores: `SqliteVectorStore` (default) and `QdrantVectorStore` (optional, loopback only). Both pass the same parity tests, the Qdrant one against an in-memory REST fake. A live-Qdrant test exists but only runs when `CODEDOTNET_QDRANT_URL` is set. It has **not** been run.

Quality is measured by the offline harness in [`evaluation.md`](evaluation.md). No quality or latency numbers are claimed.
