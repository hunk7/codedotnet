# ADR-011: RAG storage and retrieval

## Status

Proposed

## Context

The research proposal (`docs/research/codedotnet-ai-zero-cost-feasibility-and-engineering-plan.md`) chooses Qdrant plus SQLite. On a single 32 GB laptop that must also run a model, a separate vector server is extra memory and operations. The project also needs exact-identifier search, which favors lexical search.

## Decision

1. Default store: **SQLite** for metadata and cache, **FTS5** for lexical search, **sqlite-vec** for vectors. Hybrid retrieval fuses lexical and vector candidates.
2. Hide storage behind `IVectorStore`.
3. **Qdrant is an optional adapter**, CPU mode only, enabled explicitly (proposed script switch `-WithQdrant`). It is not required and not the first implementation.
4. Benchmark BM-08 compares the two stores on the target device; the default changes only through a new ADR if results justify it.
5. Index manifest records embedding model, digest and dimension; mixed dimensions are forbidden; a change means a new versioned index.
6. PDF/DOCX ingestion is postponed past v0.1.

## Alternatives considered

| Option | Why not chosen as default |
|---|---|
| Qdrant first | Extra process and RAM; Windows packaging unverified |
| Pure FTS5 | Loses semantic matches |
| In-memory vectors | Rebuild cost, no durability |

## Trade-offs

sqlite-vec is young and its Windows/.NET loading is unverified; vector scale is bounded by a single machine.

## Consequences

Backups are a single SQLite file; vectors are rebuildable from sources.

## Validation required

Phase 1 spike for sqlite-vec on Windows .NET 10; BM-07 (retrieval latency) and BM-08 (store comparison); retrieval quality on the golden dataset.
