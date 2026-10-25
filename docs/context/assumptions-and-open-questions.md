# CodeDotNet: Assumptions and Open Questions

## Assumptions (unverified)

| ID | Assumption | How to verify |
|---|---|---|
| A-01 | Target device: Windows, Ryzen 7 PRO 8840U (8C/16T), Radeon 780M, 32 GB DDR5-5600, about 502 GB free | `Invoke-HardwareProbe.ps1` [P] |
| A-02 | CPU-only inference is viable for a 1.5B-7B Q4 model at interactive speed | Benchmarks BM-01..BM-04 |
| A-03 | Ollama can use the 780M via Vulkan | Not assumed to work; probe and benchmark. CPU is the baseline |
| A-04 | `qwen2.5-coder` (1.5b, 7b) and `nomic-embed-text` are suitable and license-compatible | Check model cards/licenses at install time; names are from memory |
| A-05 | Ollama honours `OLLAMA_MAX_LOADED_MODELS` and `OLLAMA_NUM_PARALLEL` and per-request `num_ctx`, `num_gpu`, `keep_alive`, `num_predict` | Check current Ollama docs/version |
| A-06 | sqlite-vec loads on Windows .NET 10 with a suitable SQLite build | Phase 1 spike |
| A-07 | An HTTPS Pages page can call `http://127.0.0.1` in target browsers | Browser test before Phase 1 |
| A-08 | Qdrant has a usable Windows binary or container for CPU mode | Verify before the optional adapter |
| A-09 | Vite dev server default port 5173 (no explicit port in `vite.config.ts`) | Run dev server |
| A-10 | Port 7901 is free for `CodeDotNet.Ai` | Make configurable; probe at start |

## Open questions

| ID | Question | Owner | Needed by |
|---|---|---|---|
| Q-01 | Is the feasibility document to be committed under `docs/research/`? It is untracked | Maintainer | This PR review |
| Q-02 | Minimum supported browsers for the loopback HTTP call | Maintainer | Phase 1 |
| Q-03 | Is Docker allowed on the target device (only relevant to Qdrant)? | Maintainer | Qdrant adapter |
| Q-04 | Is PDF/DOCX ingestion required before v0.1? Plan postpones it | Maintainer | Phase 3 |
| Q-05 | Which license policy applies to recommended models? | Maintainer | Model governance |
| Q-06 | Should the sandbox move into the `.slnx` or a CI job of its own? | Maintainer | Phase 0 |
| Q-07 | Self-hosted runner available for real-model benchmarks? | Maintainer | Phase 6 |

## Not read in full (lower confidence)

`App.tsx` handlers, `McpPlayground.tsx`, `complexity.ts` internals, `docs/research/testing.md`, `dotnet-web-worker-notes.md`, `# CODEDOTNET_PROJECT_APPROACH.md`, most of the sandbox SRS, `docs/custom-domains.md`, ADRs 001-004 and 006-008 bodies.
