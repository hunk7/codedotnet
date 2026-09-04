# ADR-005: Dedicated Web Worker

## Status

Accepted

## Decision

Run compilation and execution inside a dedicated, disposable Web Worker rather than on the main
UI thread.

## Reasons

- Responsive UI: the main thread stays free to render and respond to input while compilation and
  execution run.
- Infinite-loop recovery: a hung/runaway program's worker can be hard-terminated
  (`WorkerManager.stop()`) and replaced with a fresh worker (`WorkerManager.recover()`) without
  reloading the page, verified in
  [`workerManager.test.ts`](../../codedotnet.web/src/workers/__tests__/workerManager.test.ts).
- Clear lifecycle: the worker has explicit states (`Uninitialized`, `Ready`, `Busy`, `Stopped`,
  etc.) that the UI can react to directly.
- Background runtime initialization: the .NET WebAssembly runtime and Roslyn can load/warm up
  without blocking the initial page render.

## Trade-off

This is a thread-isolation boundary, not a perfect security sandbox independent of the
browser/WebAssembly engine — see [`docs/security.md`](../security.md).
