# AI Architecture

Status: **Implemented (Phases 0-8).** See [`plan-validation-report.md`](plan-validation-report.md) for what was and was not verified.

```
Browser (React) --HTTP+SSE, bearer--> CodeDotNet.Ai (127.0.0.1:7901) --HTTP--> Ollama (127.0.0.1:11434)
        |                                  |  \--> SQLite (FTS5 + vectors)  or  Qdrant (127.0.0.1:6333, optional)
        \--WebSocket MCP--> codedotnet-sandbox (127.0.0.1:7900) <-- AI service (validation/execution)
```

| Component | Responsibility |
|---|---|
| `CodeDotNet.Ai` | Loopback ASP.NET Core service: pairing, security middleware, RAG, code workflows, bounded agent, CLI (`gate`, `eval`). |
| `CodeDotNet.Ai.Abstractions` | `IChatProvider`, `IEmbeddingProvider`, `IVectorStore`, `ISandboxClient`, shared records. |
| `CodeDotNet.Engine` | Roslyn compilation reused for diagnostics. The AI service never executes user code itself. |
| `codedotnet-sandbox` | The only code-execution boundary (`compile_and_run`). |
| `codedotnet.web/src/ai` | Optional "AI (local)" view. The playground works without it. |

Decisions: ADR 009-015 in [`../adr/`](../adr/README.md). The default store is SQLite (exact cosine over float32 blobs). Qdrant is an optional adapter chosen with `Ai:VectorStore=qdrant`. If the vector store or embedding provider is unavailable, retrieval falls back to lexical search only.

Profiles (`low-resource`, `recommended`) and model names are provisional configuration, not measurements; see [`hardware-and-resource-profiles.md`](hardware-and-resource-profiles.md).
