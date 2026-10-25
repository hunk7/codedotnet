# CodeDotNet: Component Catalog

**[V]** verified, **[P]** proposed.

| Component | Path | Type | Inputs | Outputs | Depends on | Tests | State |
|---|---|---|---|---|---|---|---|
| `CompileAndRunResponse` | `CodeDotNet.Contracts/` | DTO | n/a | JSON (camelCase) | n/a | via Engine tests | [V] |
| `DiagnosticInfo` | `CodeDotNet.Contracts/` | DTO | n/a | JSON | n/a | via Engine tests | [V] |
| `CodeDotNetJsonContext` | `CodeDotNet.Contracts/` | Source-generated JSON | DTOs | JSON | System.Text.Json | indirect | [V] |
| `CompilerHost` | `CodeDotNet.Engine/CompilerHost.cs` | Roslyn host | `string source` | `CompileResult` (assembly bytes, diagnostics) | Roslyn 4.14.0, Basic.Reference.Assemblies.Net100 | `CompilerHostTests` | [V] |
| `ExecutionHost` | `CodeDotNet.Engine/ExecutionHost.cs` | Runner | assembly bytes, stdin | `ExecutionResult` | reflection, Console | `ExecutionHostTests` | [V] |
| `CompileAndRunOrchestrator` | `CodeDotNet.Engine/CompileAndRunOrchestrator.cs` | Orchestrator | source, stdin | `CompileAndRunResponse` | CompilerHost, ExecutionHost | `CompileAndRunOrchestratorTests` | [V] |
| `RuntimeInfo` | `CodeDotNet.Engine/`, `codedotnet.compiler/` | Info | n/a | `RuntimeInformationPayload` | n/a | not confirmed | [V] |
| `CompileAndRunHost` | `codedotnet.compiler/CompileAndRunHost.cs` | JS interop | JS strings | JSON string | Engine, Contracts | none (browser/sandbox E2E only) | [V] |
| `worker.js` | `codedotnet.compiler/wwwroot/worker.js` | Web Worker | postMessage | typed envelopes | `_framework/dotnet.js` | `scripts/validate-worker.cjs` (not confirmed in CI) | [V] |
| `WorkerManager` | `codedotnet.web/src/workers/workerManager.ts` | TS class | requests | responses, state | protocol.ts | `workerManager.test` | [V] |
| `protocol.ts` | `codedotnet.web/src/workers/protocol.ts` | TS types | n/a | n/a | n/a | `protocol.test` | [V] |
| `McpClient` | `codedotnet.web/src/mcp/mcpClient.ts` | TS WS client | URL, JSON-RPC | results | WebSocket | `mcpClient.test` | [V] |
| `McpPlayground` | `codedotnet.web/src/components/McpPlayground.tsx` | React | user input | JSON-RPC log | McpClient | covered partly by `App.test` (lower confidence) | [V] |
| `complexity.ts` | `codedotnet.web/src/complexity.ts` | Heuristic | source | estimate | none | `complexity.test` | [V] |
| `editorSettings.ts` | `codedotnet.web/src/editor/editorSettings.ts` | Settings | localStorage | settings | none | `editorSettings.test` | [V] |
| `mcpCore.mjs` | `codedotnet-sandbox/src/` | Dispatcher | JSON-RPC | JSON-RPC | WorkerPool | `server.test.mjs`, `wsServer.test.mjs` | [V] |
| `workerPool.mjs` | `codedotnet-sandbox/src/` | Process pool | source, stdin, timeout | result | `worker.mjs` | same | [V] |
| `worker.mjs` | `codedotnet-sandbox/src/` | Child worker | IPC | IPC | published wasm | same | [V] |
| `wsServer.mjs` | `codedotnet-sandbox/src/` | WS transport | WS frames | WS frames | ws, mcpCore | `wsServer.test.mjs` | [V] |
| `server.mjs` | `codedotnet-sandbox/src/` | stdio transport | stdin lines | stdout lines | mcpCore | `server.test.mjs` | [V] |
| `deploy.yml` | `.github/workflows/` | CI/CD | push/PR | Pages | .NET 10, Node 22 | n/a | [V] |
| `CodeDotNet.Ai` | `CodeDotNet.Ai/` | Service | HTTP | JSON, SSE | Engine, Contracts, Ollama, SQLite | `CodeDotNet.Ai.Tests` | [P] |
| `IModelProvider` | `CodeDotNet.Ai.Abstractions/` | Interface | prompts | tokens | n/a | contract tests | [P] |
| `IVectorStore` | `CodeDotNet.Ai.Abstractions/` | Interface | vectors | hits | n/a | contract tests | [P] |
| Dev scripts | `scripts/dev/` | PowerShell | env | process/health | Windows | script tests | [P] |

## Reuse notes

- `CodeDotNet.Engine` is plain `net10.0`, so `CodeDotNet.Ai` can reuse `CompilerHost` natively. It currently exposes only compiler diagnostics; semantic-model and analyzer evidence would be new code **[P]**.
- `mcpCore.mjs` needs a tool registry before additional tools can be exposed **[P]**.
- Test names above come from file names read in the inventory; individual test counts were not independently run.
