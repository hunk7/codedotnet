# Testing

## Frontend (`codedotnet.web`)

Run all commands from the `codedotnet.web` directory.

| Command | Purpose |
|---|---|
| `npm test` | Runs the Vitest suite once (`vitest run`). |
| `npm run test:watch` | Runs Vitest in watch mode during development. |
| `npm run typecheck` | Runs `tsc -b --pretty false` (no emit) to verify the project compiles with strict TypeScript. |
| `npm run lint` | Runs ESLint across the project. |
| `npm run format:check` | Verifies Prettier formatting without writing changes. |
| `npm run build` | Full production build: typecheck, then `vite build`. |

### What the frontend test suite covers

- `src/__tests__/App.test.tsx` — App shell smoke tests: loading state while the worker
  initializes, and presence of Run/Stop/Clear/Reset/Download/Settings/About controls once ready.
- `src/__tests__/browserSupport.test.ts` — Feature-detection logic (`detectFeatureSupport`,
  `isBrowserSupported`, `getMissingRequiredFeatures`) used for the unsupported-browser fallback.
- `src/__tests__/complexity.test.ts` — Static Big-O time/space complexity estimator
  (`estimateComplexity`): simple/triple nested loops, sequential (non-nested) loops, and loops
  with non-braced single-statement bodies.
- `src/__tests__/diagnostics.test.ts` — Conversion of compiler diagnostics into Monaco markers.
- `src/__tests__/editorSettings.test.ts` — Settings/layout/theme persistence, defaults,
  validation, and migration behavior against `localStorage`.
- `src/workers/__tests__/protocol.test.ts` — Worker request/response message shape and protocol
  version handling.
- `src/workers/__tests__/workerManager.test.ts` — Worker lifecycle behavior using a fake `Worker`:
  verifies that `stop()` terminates a hung worker (e.g. a user program stuck in an infinite loop)
  and rejects the in-flight request, and that `recover()` produces a fresh `Ready` worker.

## .NET (`codedotnet.compiler` / `codedotnet.compiler.tests`)

Run from the repository root (or open `codedotnet.slnx` in Visual Studio and use Test Explorer).

```powershell
dotnet test codedotnet.compiler.tests/codedotnet.compiler.tests.csproj
dotnet test codedotnet.compiler.tests/codedotnet.compiler.tests.csproj -c Release
```

`codedotnet.compiler.tests` is a normal (non-browser-wasm) `net10.0` xUnit project that links
`CompilerHost.cs` and `ExecutionHost.cs` directly from `codedotnet.compiler`, because the main
project targets `RuntimeIdentifier=browser-wasm` and cannot be referenced by a desktop test host.
This lets the compiler/execution logic run natively and quickly under a normal test runner and
CI, while the actual browser-wasm worker build is still verified separately via the frontend
build and manual/CI smoke checks.

### What the .NET test suite covers

- `CompilerHostTests.cs`:
  - Successful compilation of a simple program.
  - The curated reference surface compiles successfully: `System.Collections.Generic`,
    `System.Linq`, `System.Text` (`StringBuilder`), `System.Text.Json`, `System.Threading.Tasks`
    (`async`/`await`), and `System.Numerics` (`Vector2`).
  - Syntax errors produce a diagnostic with an accurate id, message, and line/column span.
  - Empty source produces diagnostics rather than throwing.
  - An undeclared identifier produces `CS0103`.
  - Warning-only source still compiles successfully and still reports the warning diagnostic.
- `ExecutionHostTests.cs`:
  - Console output is captured for a simple program.
  - Buffered STDIN is correctly consumed by a program reading multiple lines.
  - An unhandled exception is captured (type/message/exit code) rather than propagating out of
    `ExecutionHost.Run`.
  - Output exceeding the 100,000-character limit is truncated and flagged.
  - Output within the limit is not truncated.

## CI

The [`deploy.yml`](../.github/workflows/deploy.yml) workflow's validation job runs the equivalent
of the commands above (Node and .NET restore, typecheck, lint, format check, frontend tests,
.NET tests, production build, and artifact-size checks) on every pull request, and repeats them
before deploying `main` to GitHub Pages. Because `codedotnet.compiler.tests` references
`Microsoft.NET.Test.Sdk`, the workflow's existing `dotnet test` discovery step picks it up
automatically without any workflow changes.
