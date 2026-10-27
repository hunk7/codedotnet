# CodeDotNet: Security and Privacy Baseline

**[V]** verified in source; **[P]** proposed; **[R]** risk.

## 1. Trust boundaries [V]

1. Untrusted user C# code runs in the browser Web Worker (wasm) or a Node child process (sandbox).
2. The static site is served from GitHub Pages; there is no backend.
3. The local sandbox listens on `127.0.0.1:7900` by default.

## 2. Current controls [V]

| Control | Where | Detail |
|---|---|---|
| CSP meta tag | `codedotnet.web/index.html` | `script-src 'self' 'wasm-unsafe-eval'`, `style-src 'self' 'unsafe-inline'`, `worker-src 'self' blob:`, `connect-src 'self' ws://127.0.0.1:* ws://localhost:*` |
| No telemetry | ADR 007, `docs/privacy.md` | No analytics; no `fetch`/XHR in `src/` |
| Unsafe code disabled | `CompilerHost` | `allowUnsafe: false` |
| Output cap | `ExecutionHost` | 100,000 characters |
| Hard timeout | `workerPool.mjs` | SIGKILL, default 10 s, worker respawned |
| Loopback bind | `wsServer.mjs` | `HOST` defaults to `127.0.0.1` |

## 3. Gaps [V]

| ID | Gap | Impact |
|---|---|---|
| SEC-01 | `wsServer.mjs` allows any Origin when `MCP_ALLOWED_ORIGIN` is unset (null or `*`) | A malicious web page can call `compile_and_run` on a user's machine (cross-site WebSocket hijack) |
| SEC-02 | `timeoutMs` has no upper bound | Pool starvation |
| SEC-03 | No `maxPayload` override, no input size limit | Memory pressure |
| SEC-04 | No memory ceiling for child workers | Resource exhaustion |
| SEC-05 | No MCP cancellation | Cannot abort a run early |
| SEC-06 | `poolSize` is not configurable from the environment | Operational limitation |
| SEC-07 | `docs/security.md` says `connect-src 'self'`, but `index.html` also allows loopback `ws://` | Doc/CSP mismatch |
| SEC-08 | `docs/privacy.md` states no WebSocket/fetch and "never sent to any server" | Becomes inaccurate with local AI |
| SEC-09 | Sandbox tests not in CI; Dependabot omits `/codedotnet-sandbox` and `CodeDotNet.*` | Unmonitored dependencies |
| SEC-10 | `THIRD-PARTY-NOTICES.md` omits `ws`, `Basic.Reference.Assemblies` and others (lower confidence) | License hygiene |
| SEC-11 | stdout and stderr are merged | Reduced diagnostic fidelity |

The sandbox deferred list in `docs/research/IMPLEMENTATION_STATUS.md` also names the capability model, resource ceilings and structured audit logging.

## 4. Proposed controls for the AI service [P]

- Bind `127.0.0.1` only; refuse non-loopback hosts.
- Pairing code printed by the start script, exchanged for a short-lived session token.
- Strict Origin allowlist; CORS deny-by-default; reject requests without a valid token.
- No raw prompts, source code or secrets in logs or telemetry.
- Agent tools limited to a typed allowlist; no shell, arbitrary network or filesystem tools.
- Model-license review before any model is documented as recommended.
- Local data in `%LOCALAPPDATA%\CodeDotNet\ai\` with delete/export controls.
- Pin CSP additions to `http://127.0.0.1:*` and `http://localhost:*` only.

## 5. First hardening PR (after approval) [P]

Origin default-deny (non-browser clients without Origin still allowed), `timeoutMs` cap, payload cap, `MCP_POOL_SIZE`, sandbox tests in CI, Dependabot coverage, CSP/doc corrections, `TimedOut` in `protocol.ts`. None of this is done in this docs-only PR.

## 6. Privacy statement impact [P]

When AI ships, `docs/privacy.md` must say: processing is local; data is sent only to loopback services; no cloud provider is used by default; any non-local provider is opt-in and off by default.
