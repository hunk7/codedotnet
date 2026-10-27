# CodeDotNet Platform — Implementation Status

This document tracks what has been implemented against the full program plan
("Engine → Browser Host → MCP Sandbox Host") and what remains for follow-up work.

## Completed

### Level 0 — Feasibility spike (hard gate)
- Verified the browser-wasm artifact boots and executes under plain Node.js
  (`scripts/spike-node-wasm.mjs`, since removed; see git history).
- Verified hard timeouts require child-process `SIGKILL`; in-process `Promise.race`
  cannot preempt a synchronous infinite loop inside the wasm runtime
  (`scripts/spike-node-child-timeout.mjs`, since removed; see git history).
- Findings: `docs/research/SPIKE_FINDINGS.md`. **Decision: GO.**

### Shared Contracts (`CodeDotNet.Contracts/`)
- `DiagnosticInfo`, `CompileResult`, `ExecutionResult`, `CompileAndRunResponse`,
  `RuntimeInformationPayload` — stable, camelCase-serialized DTOs shared by every host.
- `CodeDotNetJsonContext` — single source-generated JSON context used by both the browser
  worker and (indirectly, via the same compiled artifact) the MCP sandbox worker.

### Shared Engine (`CodeDotNet.Engine/`)
- `CompilerHost.Compile` — Roslyn compilation with the curated FR-043 reference surface
  (`Basic.Reference.Assemblies.Net100`), extracted verbatim from the former
  `codedotnet.compiler/CompilerHost.cs` with all platform-safety comments preserved.
- `ExecutionHost.Run` — assembly load + entry-point invocation with console output capture,
  100,000-character output truncation, and exception capture.
- `CompileAndRunOrchestrator.CompileAndRun` — the single orchestration path (compile →
  execute → status classification) now shared by every host, replacing logic that used to
  live directly inside the browser's `[JSExport]` method.
- `RuntimeInfo.GetRuntimeInformation` — runtime/Roslyn version reporting.
- `CodeDotNet.Engine.Tests/` — migrated and expanded xUnit suite (20 tests) exercising
  compiler diagnostics, execution capture/truncation, and end-to-end orchestration status
  codes. Required `xunit.runner.json` (`parallelizeAssembly`/`parallelizeTestCollections`:
  `false`) because `Console.SetOut`/`SetIn` are process-global state that is unsafe to
  mutate from parallel test collections.

### Browser host (`codedotnet.compiler/`)
- `CompileAndRunHost.CompileAndRun` and `RuntimeInfo.GetRuntimeInformation` are now thin
  `[JSExport]` adapters: they call into `CodeDotNet.Engine` and serialize the result via
  `CodeDotNetJsonContext`. No compile/execute logic remains in the browser-wasm project.
- Verified via the Node spike script that the republished artifact produces the identical
  JSON shape the frontend (`codedotnet.web/src/workers/protocol.ts`) expects — no protocol
  version bump was required.

### WorkerManager timeout-leak fix (`codedotnet.web/src/workers/workerManager.ts`)
- `compileAndRun` now clears the timeout handle in a `finally` block after
  `Promise.race` settles. Previously, a successful (or otherwise-failed) request left the
  `setTimeout` running; when it later fired, it called `stop()`/`initialize()` on an
  already-idle, unrelated worker, producing a spurious reinitialize/state flap.
- Added a regression test (`workerManager.test.ts`, fake timers) proving the first worker
  survives well past the original timeout window after a successful response.

### MCP sandbox host (`codedotnet-sandbox/`)
- `src/worker.mjs` — child-process worker that boots the published wasm artifact once and
  services `compile_and_run` requests over IPC.
- `src/workerPool.mjs` — pre-warmed pool (default size 2) of disposable child-process
  workers; enforces a hard per-request timeout via `SIGKILL` (validated by the Level 0
  spike) and automatically replaces killed/crashed workers.
- `src/server.mjs` — dependency-free stdio JSON-RPC 2.0 server implementing the MCP
  method subset needed for tool discovery and invocation (`initialize`, `tools/list`,
  `tools/call`, `shutdown`), exposing a single `compile_and_run` tool.
- `test/server.test.mjs` — end-to-end smoke tests (Node's built-in test runner, no new
  dependencies) verifying tool discovery, successful execution, and that an infinite loop
  is hard-killed within its configured timeout.
- Reuses the exact same published wasm artifact as the browser host — no separate .NET
  build target was introduced.
- `src/mcpCore.mjs` — shared tool metadata + JSON-RPC dispatch extracted out of
  `server.mjs` so both the stdio transport and the new WebSocket transport implement
  identical `initialize`/`tools/list`/`tools/call`/`shutdown` behavior.
- `src/wsServer.mjs` — WebSocket transport (`npm run serve`, default
  `ws://127.0.0.1:7900`) exposing the same dispatch for browser clients, which cannot
  spawn stdio child processes. Origin-restricted via `MCP_ALLOWED_ORIGIN`.
- `test/wsServer.test.mjs` — end-to-end smoke tests for the WebSocket transport
  (tool discovery, successful execution, hard-timeout), mirroring `test/server.test.mjs`.
- `mcp.json` — standard MCP server manifest (stdio command + tool summary) for
  registering the server with MCP clients (VS Code, Claude Desktop, etc); documented in
  `codedotnet-sandbox/README.md`.

### Browser MCP Playground (`codedotnet.web/src/mcp/`)
- `mcpClient.ts` — minimal JSON-RPC-over-WebSocket client (request/id correlation,
  connection-state tracking), unit-tested with a fake WebSocket (5 tests).
- `McpPlayground.tsx` — new in-app view: connect to a running `wsServer.mjs`, discover
  tools via `tools/list`, edit C# source in the existing Monaco editor, invoke
  `compile_and_run`, and inspect both the parsed result (status/output/diagnostics/
  timings) and the raw JSON-RPC response.
- Wired into `App.tsx` as an "Editor" / "MCP Playground" tab toggle in the toolbar, so
  the capability can be validated and showcased without leaving the existing app.

## Deferred (not implemented in this pass)

These are explicitly out of scope for this pass and are natural next steps:

- **Capability/permission model** for MCP tools (e.g. restricting which reference
  assemblies or APIs a given caller may use) — FR/NFR items referencing a capability
  system in the SRS are not yet implemented.
- **Memory/resource ceilings** beyond wall-clock timeout (e.g. per-worker memory limits
  enforced via OS primitives/cgroups equivalents on Windows).
- **Structured audit logging / observability** for the sandbox (request logs, metrics,
  tracing).
- **Full MCP SDK protocol compliance** — the current server implements a minimal,
  dependency-free subset (enough for `tools/list`/`tools/call`); it does not implement
  resources, prompts, cancellation notifications, or other parts of the full MCP spec.
  Adopting `@modelcontextprotocol/sdk` directly is recommended once network/package
  access is available, to replace the hand-rolled JSON-RPC framing.
- **CI/CD hardening**: CodeQL, dependency review, SBOM generation, build provenance
  attestation are not yet added to `.github/workflows/deploy.yml`.
- **VitePress documentation portal** and broader OSS-readiness artifacts (CONTRIBUTING,
  issue templates beyond what may already exist, etc.) have not been scaffolded.
- **Soak tests / benchmarks** across both hosts have not been implemented.
- **Built-in test harness** (in-memory xUnit-style harness for user-submitted test
  scenarios, mentioned as a v0.2 target) is not implemented.

## How to validate this state

```powershell
# .NET: build + Engine tests
dotnet build codedotnet.slnx
dotnet test CodeDotNet.Engine.Tests/CodeDotNet.Engine.Tests.csproj

# Browser worker: republish after any Engine/Contracts change
dotnet publish -c Release codedotnet.compiler/codedotnet.compiler.csproj

# Frontend: WorkerManager regression tests
cd codedotnet.web
npx vitest run src/workers/__tests__/workerManager.test.ts

# MCP sandbox: end-to-end smoke tests (requires the publish step above to have run)
cd codedotnet-sandbox
node --test test/server.test.mjs
node --test test/wsServer.test.mjs

# Browser MCP Playground: unit tests + manual end-to-end smoke test
cd ../codedotnet.web
npx vitest run src/mcp/__tests__/mcpClient.test.ts
# Terminal A: cd codedotnet-sandbox && npm run serve
# Terminal B: cd codedotnet.web && npm run dev, then open the MCP Playground tab,
# Connect, and Call compile_and_run.
```
