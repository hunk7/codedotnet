# Architecture

This document summarizes the codedotnet system architecture and component responsibilities
(see §12–§13 of [`docs/codedotnet-project-requirements-and-architecture.md`](codedotnet-project-requirements-and-architecture.md)
for the full requirements), and maps each responsibility to the actual implementation.

## 12.1 Architecture style

codedotnet is a static single-page application with a client-side, worker-based compiler and
runtime. There is no backend service.

```plaintext
GitHub Pages
     │
     │ static assets
     ▼
Browser Main Thread
     │
     ├── React UI                     (codedotnet.web/src/App.tsx)
     ├── Monaco Editor                (codedotnet.web/src/components/MonacoEditor.tsx)
     ├── Layout and settings          (codedotnet.web/src/editor/editorSettings.ts)
     ├── Local persistence            (localStorage, via editorSettings.ts)
     └── Execution coordinator        (App.tsx: handleRun/handleStop + WorkerManager)
              │
              │ typed postMessage protocol
              ▼
        Dedicated Web Worker          (codedotnet.web/src/workers/workerManager.ts + worker script)
              │
              ├── .NET 10 WebAssembly runtime
              ├── Roslyn compiler             (codedotnet.compiler/CompilerHost.cs)
              ├── Framework metadata references (CompilerHost.BuildReferences)
              ├── In-memory assembly           (CompilerHost.Compile -> MemoryStream)
              ├── Execution host               (codedotnet.compiler/ExecutionHost.cs)
              └── Console/diagnostic capture   (ExecutionHost.CallbackTextWriter)
```

## 12.2 Stack in use

### Frontend

- React 19 + TypeScript (strict mode) + Vite
- Monaco Editor (`codedotnet.web/src/components/MonacoEditor.tsx`)
- Native pointer-event-based resizable split panes (`App.tsx` divider handlers) rather than a
  third-party resizable-panels library
- Plain CSS (`App.css`) with a `data-theme` attribute for light/dark theming
- Static Big-O complexity estimator (`codedotnet.web/src/complexity.ts`) that analyzes the C#
  source per method (loop nesting via matched-paren/brace interval tracking, recursion shape,
  memoization, divide-and-conquer/halving, LINQ, hash vs. linear lookups) and renders a time/space
  estimate with rationale in the output pane after each run

### Runtime and compiler

- .NET 10 WebAssembly runtime (`codedotnet.compiler`, `RuntimeIdentifier=browser-wasm`)
- Roslyn (`Microsoft.CodeAnalysis.CSharp`) via `CompilerHost.cs`
- Curated reference assemblies resolved from `TRUSTED_PLATFORM_ASSEMBLIES` (`CompilerHost.BuildReferences`)
- `[JSExport]`-based JS/.NET interop (`codedotnet.compiler/CompileAndRunHost.cs`, `RuntimeInfo.cs`)

### Worker and communication

- Dedicated Web Worker managed by `codedotnet.web/src/workers/workerManager.ts`
- Structured-clone-compatible request/response messages defined in
  `codedotnet.web/src/workers/protocol.ts`, including a `protocolVersion` field and correlation
  (`requestId`) for stale-message rejection
- Worker lifecycle: `initialize()`, `compileAndRun()`, `stop()`, `recover()` — see
  `codedotnet.web/src/workers/__tests__/workerManager.test.ts` for verified stop/recover behavior
  against a hung ("infinite loop") execution

### Persistence

- `localStorage` for source, STDIN, editor settings, layout settings, and theme
  (`codedotnet.web/src/editor/editorSettings.ts`)
- No IndexedDB usage; not required at this project's scale

### Testing

- Vitest + React Testing Library for the frontend (`codedotnet.web/src/**/__tests__/*.test.ts(x)`)
- Full Playwright end-to-end coverage is intentionally deferred for v1 (see
  [`docs/security.md`](security.md) and project scope notes) in favor of Vitest/RTL component
  and unit coverage plus manual verification
- `codedotnet.compiler.tests` (xUnit) exercises `CompilerHost` and `ExecutionHost` directly by
  linking their source files, since the main `codedotnet.compiler` project targets
  `browser-wasm` and cannot be referenced by a normal desktop test host

### CI/CD

- GitHub Actions workflow (`.github/workflows/deploy.yml`): PR validation and main-branch
  deployment to GitHub Pages
- Dependabot (`.github/dependabot.yml`): weekly npm, NuGet, and GitHub Actions updates

## 13. Component responsibilities

| Component | Responsibilities | Implementation |
|---|---|---|
| Application Shell | Root layout, global theme, boot status, error boundary, modal host | `App.tsx`, `components/ErrorBoundary.tsx` |
| Toolbar | Runtime status, Run/Stop, Clear/Reset/Download, pane swap, focus/fullscreen, theme/settings, About | `App.tsx` toolbar section, `components/SettingsPanel.tsx`, `components/AboutDialog.tsx` |
| Monaco Editor Adapter | Monaco loading, `Program.cs` model lifecycle, preferences, diagnostic markers, error navigation, keyboard commands, disposal | `components/MonacoEditor.tsx`, `diagnostics.ts` |
| I/O Panel | STDIN editing, output rendering, problems list, execution status, timings, output clearing/truncation indicator | `App.tsx` I/O pane section |
| Layout Manager | Pane ratios/order, nested I/O ratio, focus modes, fullscreen, keyboard divider movement, persistence, default restoration | `App.tsx` layout state + `editor/editorSettings.ts` |
| Settings Service | Default settings, validation, schema version, migration, persistence, reset | `editor/editorSettings.ts` |
| Execution Coordinator | Execution state machine, request creation/correlation, run lock, timeout, stop, worker recovery, result aggregation | `App.tsx` (`handleRun`/`handleStop`), `workers/workerManager.ts` |
| Worker Manager | Worker creation, runtime init, message transport, failure detection, termination/replacement, stale-message rejection | `workers/workerManager.ts` |
| Compiler Host | Parse source, configure Roslyn, load/reuse references, emit in-memory assembly, diagnostics conversion | `codedotnet.compiler/CompilerHost.cs` |
| Execution Host | Load assembly, provide STDIN, capture stdout/stderr, execute entry point, capture exceptions, report timings | `codedotnet.compiler/ExecutionHost.cs` |

See also: [`docs/security.md`](security.md) for the security model and CSP, and
[`docs/privacy.md`](privacy.md) for the data-handling and privacy-verification procedure.

## 14. MCP Playground (optional, out-of-browser-sandbox component)

Unlike the fully client-side playground above, the **MCP Playground** tab talks to an external
Node.js process (`codedotnet-sandbox/`) implementing the Model Context Protocol:

```plaintext
Browser (MCP Playground tab)
     │
     │ WebSocket, JSON-RPC 2.0 (initialize / tools/list / tools/call)
     ▼
codedotnet-sandbox/src/wsServer.mjs
     │
     ▼
codedotnet-sandbox/src/mcpCore.mjs      <- shared tool metadata + dispatch (also used by stdio transport)
     │
     ▼
codedotnet-sandbox/src/workerPool.mjs   <- disposable, hard-timeout-enforced child processes
     │
     ▼
codedotnet-sandbox/src/worker.mjs (child process)
     │
     ▼
codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js
     (the same published CodeDotNet.Engine wasm artifact used by the in-browser Web Worker)
```

Key properties:

- `src/mcp/mcpClient.ts` is a small JSON-RPC-over-WebSocket client used only by the MCP Playground
  tab; it does not affect the default Editor view or its Web Worker execution path.
- The server reuses the same compiled `CodeDotNet.Engine` artifact as the browser, so
  `compile_and_run` results are consistent between the two playgrounds.
- Each `compile_and_run` call runs in its own disposable child process with a hard timeout
  (`SIGKILL` on expiry), not in-process `Promise.race`, because synchronous wasm execution blocks
  the Node.js event loop (see `codedotnet-sandbox/README.md` and
  `docs/research/SPIKE_FINDINGS.md`).
- The same `tools/list`/`tools/call` dispatch (`mcpCore.mjs`) is shared between the stdio transport
  (`server.mjs`, for VS Code/Claude Desktop-style MCP clients) and the WebSocket transport
  (`wsServer.mjs`, for the browser).

See [`codedotnet-sandbox/README.md`](../codedotnet-sandbox/README.md) for isolation details, the
tool contract, and how to register the server with an MCP client.
