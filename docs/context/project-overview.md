# CodeDotNet: Project Overview

Status: context package, docs-only. Evidence level is marked per statement:
**[V]** verified in source, **[P]** proposed (does not exist), **[A]** assumption, **[R]** risk.

## 1. What CodeDotNet is today [V]

CodeDotNet is a browser-based C# playground. The user edits a single `Program.cs` in Monaco and runs it. The .NET 10 runtime and the Roslyn compiler run inside a dedicated Web Worker as WebAssembly. The site is static and is deployed to GitHub Pages. There is no application backend.

A second, separate component exists: `codedotnet-sandbox`, a local Node.js MCP server (stdio and WebSocket) that exposes one tool, `compile_and_run`, backed by disposable child processes that boot the same published WebAssembly artifact.

## 2. Goal of the AI initiative [P]

Evolve CodeDotNet into a local-first, zero-mandatory-recurring-cost, AI-assisted C# engineering and learning platform **without breaking the existing playground**. Constraints:

- The existing playground and MCP Playground must keep working with no AI service installed.
- All inference runs on the user's machine (default: Ollama, CPU-first).
- No paid API, cloud account or telemetry is required.
- This pull request is documentation only. No AI code is implemented here.

## 3. Components at a glance

| Component | Path | Role | State |
|---|---|---|---|
| Contracts | `CodeDotNet.Contracts/` | Shared DTOs and source-generated JSON context | [V] |
| Engine | `CodeDotNet.Engine/` | `CompilerHost`, `ExecutionHost`, `CompileAndRunOrchestrator` (plain `net10.0`) | [V] |
| Engine tests | `CodeDotNet.Engine.Tests/` | xUnit tests for the engine | [V] |
| Browser-wasm host | `codedotnet.compiler/` | `[JSExport]` adapter and `wwwroot/worker.js` | [V] |
| Frontend | `codedotnet.web/` | React 19, TypeScript, Vite, Monaco, Vitest | [V] |
| MCP sandbox | `codedotnet-sandbox/` | Node MCP server, WebSocket on 127.0.0.1:7900 by default | [V] |
| AI orchestrator | `CodeDotNet.Ai/` (+ `.Abstractions`, `.Tests`) | Local loopback HTTP+SSE service | [P] |
| Dev scripts | `scripts/dev/` | Start, stop, health, probe, diagnostics | [P] |

## 4. Technology baseline [V]

- .NET 10 (`net10.0`; `codedotnet.compiler` targets `browser-wasm`).
- Roslyn `Microsoft.CodeAnalysis.CSharp` 4.14.0; reference assemblies `Basic.Reference.Assemblies.Net100` 1.8.11.
- React 19, TypeScript (strict), Vite 8, Monaco 0.56, Vitest 4, ESLint, Prettier.
- Node.js (`ws` is the only sandbox dependency).
- CI: `.github/workflows/deploy.yml` (validate on all pushes/PRs; deploy to GitHub Pages from `main`).

## 5. Target development device [A]

User-supplied, not independently verified by the repository: Windows, AMD Ryzen 7 PRO 8840U (8C/16T), integrated Radeon 780M, 32 GB RAM, about 502 GB free disk. See [`../ai/hardware-and-resource-profiles.md`](../ai/hardware-and-resource-profiles.md).

## 6. Where to read next

- Layout: [`repository-map.md`](repository-map.md)
- Architecture: [`current-architecture.md`](current-architecture.md)
- Plan: [`../ai/codedotnet-ai-master-implementation-plan.md`](../ai/codedotnet-ai-master-implementation-plan.md)
- Open questions: [`assumptions-and-open-questions.md`](assumptions-and-open-questions.md)
