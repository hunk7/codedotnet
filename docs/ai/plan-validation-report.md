# Plan Validation Report

## 0. Final validation (Phases 0-8, release candidate)

Run on Windows, .NET SDK 10, branch `codedotnet-mcp-platform`.

| Command | Result |
|---|---|
| `dotnet build codedotnet.slnx -c Release` | **Pass**: 0 warnings, 0 errors |
| `CodeDotNet.Ai.Tests` | **Pass**: 246 |
| `CodeDotNet.Engine.Tests` | **Pass**: 20 |
| `codedotnet-sandbox` `npm test` | **Pass**: 18 |
| `codedotnet.web` `npm test` | **Pass**: 66 (31 in `src/ai`) |
| `codedotnet.web` typecheck, format:check, build | **Pass** |
| `codedotnet.web` lint | **Pass**: 0 errors, 2 pre-existing warnings |
| `Publish-CodeDotNetAi.ps1` and smoke run of the published build | **Pass**: loopback bind, 401 without token, health `unavailable` with no Ollama |
| `CodeDotNet.Ai gate` | Exit 1 **by design**: threshold limits are null until benchmarks exist |
| `CodeDotNet.Ai eval` | Exit 0, lexical-only (no Ollama): recall@5 1.000, MRR 1.000 on the 6-case seed set. Printed, not a benchmark result |

**Not run / not verified:** a real Ollama model (chat, embeddings, citations quality); a live Qdrant (Docker daemon was not running; the live test is gated on `CODEDOTNET_QDRANT_URL`); GPU mode; HTTPS-hosted page to HTTP loopback in real browsers; end-to-end `Start-CodeDotNet.ps1`; benchmarks BM-01..BM-12. No performance or quality claim is made.

---

# Original documentation-PR validation (historical)

Scope: documentation-only PR on branch `codedotnet-mcp-platform`. Results below come from commands run during this PR. Anything not listed was **not run**.

## 1. Commands run

| Command | Result |
|---|---|
| `git branch --show-current` | `codedotnet-mcp-platform` |
| `dotnet build codedotnet.slnx -c Release` | **Pass**: 0 warnings, 0 errors |
| `dotnet test CodeDotNet.Engine.Tests/CodeDotNet.Engine.Tests.csproj -c Release` | **Pass**: 20 passed, 0 failed |
| `npm run typecheck` (`codedotnet.web`) | **Pass** |
| `npm run lint` (`codedotnet.web`) | **Pass**: 0 errors, 2 warnings (unused eslint-disable directive, pre-existing) |
| `npm run format:check` (`codedotnet.web`) | **Pass** |
| `npm test` (`codedotnet.web`) | **Pass**: 8 files, 35 tests |
| `npm test` (`codedotnet-sandbox`) | **Fail**: `node --test test/` exits 1 with `Cannot find module '...\codedotnet-sandbox\test'` under Node v22.14.0 on Windows. This is a pre-existing script problem, not caused by this PR. |
| `node --test test/server.test.mjs test/wsServer.test.mjs` (`codedotnet-sandbox`) | **Pass**: 6 tests, 0 failures (worker assets were already present locally) |
| Markdown relative-link check over `docs/context`, `docs/ai`, `docs/adr` | Only pre-existing break found: `docs/adr/008-buffered-stdin.md` -> `../../codedotnet.compiler.tests/ExecutionHostTests.cs` (stale path, not modified). Links to this report resolve once it exists. |
| JSON parse of `thresholds.json` and `result.schema.json` | Pass (parses; schema semantics not validated by a validator) |
| Path/symbol checks | `CompilerHost.cs`, `ExecutionHost.cs`, `CompileAndRunOrchestrator.cs` exist in `CodeDotNet.Engine/`; `RuntimeInfo.cs` exists in both `CodeDotNet.Engine/` and `codedotnet.compiler/`; `PORT` 7900 and `HOST` 127.0.0.1 in `wsServer.mjs`; `DEFAULT_POOL_SIZE = 2`, `DEFAULT_TIMEOUT_MS = 10_000` in `workerPool.mjs`; CSP `connect-src 'self' ws://127.0.0.1:* ws://localhost:*` in `index.html` |

## 2. Not run

- Real-model, hardware, Vulkan, Qdrant, sqlite-vec and browser loopback checks. No AI code exists.
- Any benchmark (BM-01..BM-12). No result files were created.
- Script tests (scripts do not exist).
- Markdown lint (none configured).
- `npm ci` and `dotnet publish` of worker assets (existing artifacts were used).
- CI workflow execution.

## 3. Newly discovered inconsistencies

1. `npm test` in `codedotnet-sandbox` fails on Windows/Node 22.14.0 because `node --test test/` treats `test/` as a module path; running the files explicitly works. The Phase 0 CI step must use a glob or explicit file list (for example `node --test test/*.test.mjs`; needs to be verified on Linux CI).
2. `docs/adr/008-buffered-stdin.md` links to a stale `codedotnet.compiler.tests` path.
3. The CSP in `index.html` has `default-src 'self'` and no `http://` loopback entry, which the AI service would need.
4. The feasibility document remains untracked and unchanged.

Items from the earlier inventory (stale docs paths, SEC-01..SEC-11) are in [`../context/security-and-privacy-baseline.md`](../context/security-and-privacy-baseline.md) and [`../context/documentation-index.md`](../context/documentation-index.md).

## 4. Remaining assumptions

Hardware specification; Vulkan/ROCm support on the Radeon 780M; model names and licenses (`qwen2.5-coder`, `nomic-embed-text`); Ollama environment variables and request options; Qdrant Windows packaging; sqlite-vec on Windows .NET 10; HTTPS Pages to HTTP loopback in target browsers. See [`../context/assumptions-and-open-questions.md`](../context/assumptions-and-open-questions.md).

## 5. Needs a prototype

sqlite-vec loading; browser loopback call with pairing; native vs wasm Roslyn diagnostic parity; GPU validation flow.

## 6. Needs benchmarks

Profile values, context caps, `num_thread`, embedding batch size, vector store choice (BM-08), reranker need, agent iteration limits, memory budget.

## 7. Confidence by subsystem

| Subsystem | Confidence | Reason |
|---|---|---|
| Current Engine/browser flow | High | Source read; build and tests pass |
| Sandbox | Medium-high | Source read; tests pass when run by file; hardening items verified by reading |
| CI and docs gaps | Medium | `deploy.yml` read; some docs not fully read |
| AI architecture | Medium | Proposal; no prototype |
| Hardware and profiles | Low-medium | Assumptions only; no measurements |
| Windows scripts | Low | Not written |

## 8. Scope check

Changes: new files under `docs/context/`, `docs/ai/`, `docs/adr/009..015`, and one edit to `docs/adr/README.md`. No source, CI, CSP, dependency or script changes. The feasibility document is untouched.
