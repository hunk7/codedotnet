# CodeDotNet: Testing and Quality Baseline

**[V]** verified by reading files; **[N]** not run in this PR (see [`../ai/plan-validation-report.md`](../ai/plan-validation-report.md) for what was actually executed).

## 1. Existing test suites

| Suite | Location | Runner | In CI | Notes |
|---|---|---|---|---|
| Engine tests | `CodeDotNet.Engine.Tests/` | xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0, coverlet 6.0.2 | Yes (discovery of csproj containing `Microsoft.NET.Test.Sdk`) | Parallelism disabled via `xunit.runner.json` because `Console.SetOut/SetIn` are global. `docs/research/IMPLEMENTATION_STATUS.md` claims 20 tests [N]. |
| Frontend tests | `codedotnet.web/src/**/__tests__/` | Vitest 4 + jsdom + React Testing Library | Yes | Files: `App`, `browserSupport`, `complexity`, `diagnostics`, `editorSettings`, `workers/protocol`, `workers/workerManager`, `mcp/mcpClient`. |
| Sandbox tests | `codedotnet-sandbox/test/` | `node --test` | **No** | `server.test.mjs`, `wsServer.test.mjs`. Need the published wasm artifact. The ws test client sends no Origin header. |
| Worker validation | `scripts/validate-worker.cjs` | Node | Used by build tooling (lower confidence) | |

## 2. Static quality gates [V]

- TypeScript strict; `npm run typecheck`.
- ESLint 10 and Prettier 3.9 (`npm run lint`, `npm run format:check`), scoped to `codedotnet.web`.
- No .NET analyzers configured beyond `.editorconfig` (lower confidence; not exhaustively read).
- No markdown lint is configured.

## 3. Gaps [V]

1. Sandbox tests are not in CI.
2. No browser end-to-end test of the real worker.
3. `CompileAndRunHost` has no direct test.
4. No coverage threshold.
5. Docs reference stale test project paths (`codedotnet.compiler.tests`).
6. Dependabot does not cover `/codedotnet-sandbox` or the `CodeDotNet.*` projects.

## 4. Proposed test layers for AI work [P]

| Layer | Content | Where it runs |
|---|---|---|
| Unit | chunking, hashing, citation validation, budget, redaction, config validation | PR CI |
| Contract | fake model provider and fake vector store | PR CI |
| Integration | real SQLite FTS5 + sqlite-vec, native Roslyn | PR CI (Windows or Linux) |
| Script tests | PowerShell scripts (Pester or `-WhatIf` dry run), health exit codes 0/1/2 | Windows workflow, path-filtered |
| Security regressions | Origin deny, pairing, CORS, path traversal, prompt-injection fixtures | PR CI |
| Real-model benchmarks | BM-01..BM-12 | Manual or self-hosted only |

Release thresholds are not set; `docs/ai/benchmarks/thresholds.json` holds null limits and a gate must fail while any limit is null.

## 5. Regression guard

The existing browser playground and sandbox tests must still pass unchanged for every AI PR. The first implementation PR adds sandbox tests to CI.
