# ADR-001: Browser execution instead of server execution

## Status

Accepted

## Decision

Compile and execute C# inside the browser rather than sending code to a server-side compiler.

## Reasons

- Static hosting.
- No compiler backend to build, operate, or scale.
- No account requirement for users.
- No per-run infrastructure cost.
- Reduced server attack surface (there is no server).
- Source-code privacy: user code never leaves the browser.
- Natural client-side scaling — each user's browser does its own work.

## Trade-offs

- Large initial download (the .NET WebAssembly runtime and Roslyn assemblies).
- Browser memory limitations bound program execution.
- Restricted OS APIs compared to a native compiler/runtime.
- No arbitrary NuGet restore (see [ADR-004](004-single-file-mvp.md) and
  [`docs/known-limitations.md`](../known-limitations.md)).
- Hard worker termination is the only reliable cancellation mechanism for runaway programs (see
  [ADR-005](005-dedicated-web-worker.md)).

## See also

- [`docs/architecture.md`](../architecture.md)
- [`docs/known-limitations.md`](../known-limitations.md)
