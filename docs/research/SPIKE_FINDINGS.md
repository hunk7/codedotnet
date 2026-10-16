# Level 0 Spike Findings — wasm-under-Node Feasibility

Status: **PASS — gate cleared.** Proceed to Level 1 (discovery documents) and onward.

## Environment

- Node.js: v22.14.0
- .NET SDK: 10.0.401
- Browser-wasm runtime produced by: `.NET 10.0.12`, RID `browser-wasm`
- Roslyn version in artifact: `4.14.0.0`

## Spike 1 — Boot and invoke the existing browser-wasm artifact under plain Node.js

**Goal:** Determine whether `codedotnet.compiler`'s published browser-wasm output (the
same artifact used by the browser Web Worker) can be loaded and executed directly under
Node.js, without a browser/worker context, using the same `dotnet.create()` /
`getConfig()` / `getAssemblyExports()` sequence as `codedotnet.compiler/wwwroot/worker.js`.

**Method:**
1. `dotnet publish -c Release codedotnet.compiler/codedotnet.compiler.csproj` →
   produced `codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js` and
   associated `.wasm` assets.
2. Script: `scripts/spike-node-wasm.mjs` (temporary, not part of shipped product).
   Imports `dotnet` from the generated `_framework/dotnet.js`, boots the runtime, gets
   assembly exports, and invokes the same `[JSExport]` methods the browser worker uses:
   `RuntimeInfo.GetRuntimeInformation()` and `CompileAndRunHost.CompileAndRun(source, stdin)`.

**Result: PASS.** All steps succeeded with no code changes to the existing compiler host.

| Step | Result | Timing |
|---|---|---|
| `dotnet.create()` | OK | 248.7 ms |
| `getConfig()` | OK | — |
| `getAssemblyExports()` | OK | 22.1 ms |
| `RuntimeInfo.GetRuntimeInformation()` | OK | 94.6 ms |
| `CompileAndRun()` — cold (first compile) | OK | 1327.1 ms total (compile 1295.8 ms, execute 14.4 ms) |
| `CompileAndRun()` — warm (second compile, same process) | OK | 34.7 ms total (compile 33.8 ms, execute 0.4 ms) |

Sample output payload (warm run):
```json
{"status":"ExecutionCompleted","diagnostics":[],"output":"Hello from Node-hosted wasm spike!\n","outputTruncated":false,"exitCode":0,"exceptionType":null,"exceptionMessage":null,"exceptionStackTrace":null,"compilationDurationMs":33.7881,"executionDurationMs":0.383,"totalDurationMs":34.1871}
```

**Interpretation:**
- Node.js can host the exact same wasm artifact used in the browser, with identical
  `[JSExport]` surface. No Node-specific build is required for Level 0 purposes; the
  existing publish output is directly consumable.
- Cold start (~250 ms runtime boot + ~1.3 s first Roslyn compile, which includes JIT/AOT
  warmup of the compiler pipeline on first use) is the dominant latency source. Warm
  compiles on an already-booted runtime are fast (~35 ms total). This has direct
  implications for the MCP worker pool design (Level 7+): workers should be kept warm
  and reused rather than booting a fresh wasm runtime per request, or a worker pool with
  pre-warmed instances should absorb the ~1.5s cold-start cost.
- No platform-specific failures, missing globals, or incompatible APIs were observed
  booting this artifact under Node 22.

## Spike 2 — Hard timeout via child-process termination

**Goal:** Confirm that a hard timeout for runaway/infinite-loop user code must be
enforced by killing the OS child process (not `Promise.race` in the same event loop),
since synchronous wasm/managed execution blocks the JS event loop and cannot be
preempted cooperatively.

**Method:**
- `scripts/spike-node-child-timeout.mjs` (parent): forks `scripts/spike-child-infinite-loop.mjs`
  as a child process, starts a 2000 ms timer, and sends `SIGKILL` if the child has not
  exited by then.
- `scripts/spike-child-infinite-loop.mjs` (child): boots the wasm runtime, then calls
  `CompileAndRunHost.CompileAndRun()` with a C# program containing `while (true) { }`,
  which never returns.

**Result: PASS.**

```json
{
  "ok": true,
  "code": null,
  "signal": "SIGKILL",
  "elapsedMs": 2281.7,
  "killedByTimeout": true
}
```

The child printed its boot/ready messages, then was observed never returning from
`CompileAndRun`. The parent's `SIGKILL` at ~2.26s terminated it reliably and immediately
(exit observed at 2.28s, i.e. within ~20ms of the kill signal — OS-level termination, not
cooperative).

**Interpretation:**
- Confirms the architectural requirement in the SRS: hard timeouts for the MCP sandbox
  **must** be implemented via child-process termination (`SIGKILL`/`taskkill`), not
  `Promise.race`/`setTimeout` racing a pending promise in the same process. A
  synchronous infinite loop inside the wasm runtime blocks the Node event loop entirely,
  so no in-process timeout mechanism (timers, promises, async racing) can preempt it.
- The disposable child-process-per-worker model from the architecture doc is validated
  as necessary, not just a defense-in-depth preference.

## Gate Decision

Both required Level 0 questions are answered with evidence:

1. **Can the existing browser-wasm artifact boot and execute under Node.js without
   modification?** Yes.
2. **Can hard timeouts be enforced against synchronous/infinite-loop user code?** Yes,
   via child-process `SIGKILL`; in-process racing is confirmed insufficient.

**Decision: GO.** Proceed with Level 1 (discovery documents), Level 2 (target
architecture/ADRs), and subsequent levels including the MCP sandbox (Levels 7–14) as
planned. No architectural pivot is required based on this spike.

## Residual risks / follow-ups for later levels

- Cold-start cost (~1.5s first compile) should be mitigated via a pre-warmed worker pool
  in the MCP supervisor (Level 7/9) rather than booting per-request.
- This spike used the existing single-file `CompileAndRunHost.CompileAndRun` entry
  point; the eventual MCP worker protocol (Level 9) will need its own message-based
  request/response framing analogous to `codedotnet.compiler/wwwroot/worker.js`, adapted
  for stdio/IPC instead of `postMessage`.
- Memory/resource ceiling enforcement (beyond timeout) was not exercised in this spike
  and remains a Level 11 concern (process-level memory limits via OS primitives).
- The temporary spike scripts (`scripts/spike-node-wasm.mjs`,
  `scripts/spike-node-child-timeout.mjs`, `scripts/spike-child-infinite-loop.mjs`) were
  kept in the repo as evidence/reproduction artifacts; they are not wired into CI and can
  be removed or relocated under `codedotnet-sandbox/` once Level 7+ scaffolding begins.
