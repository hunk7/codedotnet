# Known Limitations

codedotnet is intentionally scoped as a single-file, browser-only C# playground. This document
lists deliberate constraints of version 1 so users and contributors have accurate expectations.

## Buffered STDIN only

STDIN is provided as a single, fully-buffered text block entered before clicking **Run**
(`codedotnet.compiler/ExecutionHost.cs`, `Run(byte[] assemblyBytes, string stdin)`). There is no
interactive/streaming STDIN: a program cannot prompt the user and receive input mid-execution.
All `Console.ReadLine()`/`Console.Read()` calls consume from the pre-supplied buffer, and reading
past the end of that buffer behaves like reaching end-of-input.

## Single-file constraint

Only a single `Program.cs` file (top-level statements) is supported per project
(`codedotnet.compiler/CompilerHost.cs` parses exactly one syntax tree named `Program.cs`). There
is no multi-file project model, no ability to add additional `.cs` files, and no project/solution
concept in the UI.

## No NuGet package restore

The compiler references a curated, fixed set of framework assemblies resolved from the .NET
WebAssembly runtime's trusted platform assemblies list (`CompilerHost.BuildReferences`):
`System.Private.CoreLib`, `System.Runtime`, `System.Collections(.Generic)`, `System.Linq`,
`System.Numerics(.Vectors)`, `System.Text(.Json)`, `System.Text.RegularExpressions`,
`System.Threading(.Tasks)`, `System.Console`, `System.Runtime.Extensions`,
`System.Runtime.Numerics`, `System.ObjectModel`, `System.Linq.Expressions`, and `netstandard`.
There is no NuGet client in the browser, no package restore, and no way to reference third-party
NuGet packages. Code that depends on any package or namespace outside this curated set will fail
to compile with an unresolved-reference diagnostic.

## Browser resource limits

Because compilation and execution both happen inside a single Web Worker running the .NET
WebAssembly runtime, codedotnet inherits ordinary browser resource constraints:

- **Memory** — the WebAssembly heap and the browser tab's overall memory budget bound how much
  memory a compiled program can allocate; there is no configurable memory limit beyond what the
  browser enforces, and very large allocations can crash the worker/tab.
- **CPU / infinite loops** — a program with an infinite loop (e.g. `while (true) {}`) will hang
  the worker. The app detects this via the configured execution timeout and/or manual **Stop**,
  which terminates and replaces the worker (see `codedotnet.web/src/workers/workerManager.ts`
  `stop()`/`recover()`), but the hung worker instance itself is not gracefully cancelled — it is
  hard-terminated.
- **Output size** — console output is capped at 100,000 characters
  (`codedotnet.compiler/ExecutionHost.cs`); output beyond that limit is truncated and flagged via
  `OutputTruncated`, both to bound memory usage and to keep the UI responsive.
- **Startup time** — first load must download and initialize the .NET WebAssembly runtime and
  Roslyn assemblies, which is slower than a native compiler; subsequent runs within the same
  worker reuse the initialized runtime and cached references.

## No perfect execution sandbox guarantee

As noted in [`docs/security.md`](security.md), user code runs in a dedicated Web Worker for
thread isolation from the UI, backed by the browser's own WebAssembly sandbox — but this is not
presented as a perfect security boundary independent of the browser/WebAssembly engine's own
security guarantees.

## Deferred for a later version

- Full Playwright end-to-end test coverage (component/unit testing via Vitest + React Testing
  Library is in place; see [`docs/testing.md`](testing.md)).
- A comprehensive WCAG accessibility audit (basic keyboard operability and visible-label
  controls are implemented, per §11.4/FR-111/FR-112).
- Multi-file projects and NuGet package restore, as described above.
