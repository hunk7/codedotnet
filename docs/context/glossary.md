# CodeDotNet: Glossary

| Term | Meaning |
|---|---|
| ADR | Architecture Decision Record, in `docs/adr/` |
| CompilerHost | Roslyn wrapper in `CodeDotNet.Engine` |
| ExecutionHost | Loads and runs emitted assemblies |
| browser-wasm | .NET target running in a browser via WebAssembly |
| MCP | Model Context Protocol; here, JSON-RPC 2.0 over stdio or WebSocket |
| Sandbox | `codedotnet-sandbox`, a local Node MCP server running disposable child processes |
| Worker pool | `workerPool.mjs`, forked children with hard timeout |
| Orchestrator (AI) | Proposed `CodeDotNet.Ai` .NET service |
| Loopback | `127.0.0.1` / `localhost`; reachable only from the same machine |
| SSE | Server-Sent Events, used to stream tokens |
| Pairing code | One-time code shown by the start script and exchanged for a session token |
| Origin allowlist | Explicit list of browser origins allowed to call a local service |
| RAG | Retrieval-augmented generation |
| FTS5 | SQLite full-text search extension |
| sqlite-vec | SQLite vector search extension |
| Qdrant | Optional vector database, planned only as an opt-in CPU adapter |
| Ollama | Local model runtime, the default provider |
| Profile | Named resource configuration (for example `low-resource`, `recommended`) setting model size, context, retrieval counts, concurrency |
| Model residency | Which models are loaded in memory at the same time |
| `keep_alive` | Ollama parameter controlling how long a model stays loaded after a request |
| `num_ctx` | Ollama context-window size in tokens; affects memory |
| `num_gpu` | Ollama parameter for layers offloaded to GPU; `0` forces CPU |
| `num_predict` | Ollama maximum generated tokens |
| UMA | Unified memory architecture; an integrated GPU shares system RAM, so GPU offload does not add memory |
| Vulkan | Graphics/compute API; a possible accelerator path for AMD integrated GPUs, unproven on the target device |
| TTFT | Time to first token |
| Quantization (Q4_K_M) | Reduced-precision model weights to cut memory |
| Golden dataset | Curated evaluation cases with expected facts |
| Verified / Proposed / Assumption / Risk | Evidence labels used across these docs |
