# ADR-008: Buffered STDIN

## Status

Accepted

## Decision

Collect the complete STDIN value from the user up front and pass it to the executed program as a
single buffered string, rather than providing a fully interactive terminal.

## Reasons

- Simpler browser-worker interaction: STDIN is sent as part of the single `CompileAndRun` request
  payload instead of requiring a bidirectional streaming channel between the UI and the worker
  mid-execution.
- Predictable testing: execution behavior is deterministic given a fixed STDIN string, as
  demonstrated in
  [`ExecutionHostTests.cs`](../../codedotnet.compiler.tests/ExecutionHostTests.cs).
- Suitable for the primary use case — interview-style and algorithm-practice programs — which
  typically read a fixed set of inputs at the start of execution.
- Avoids pretending to provide a fully interactive terminal experience that the architecture
  cannot actually deliver (a program cannot prompt and block for new input mid-run).

## Related limitation

See [`docs/known-limitations.md`](../known-limitations.md) for the "buffered STDIN only"
limitation that follows from this decision.
