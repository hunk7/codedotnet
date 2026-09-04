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
