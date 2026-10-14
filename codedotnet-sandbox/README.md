# codedotnet-sandbox

A minimal Model Context Protocol (MCP) stdio server that exposes a sandboxed
`compile_and_run` tool for single-file C# programs.

## Architecture

```
MCP client (stdio, JSON-RPC 2.0)      Browser MCP Playground (WebSocket, JSON-RPC 2.0)
        |                                           |
        v                                           v
  src/server.mjs                            src/wsServer.mjs
  (stdio framing)                           (WebSocket framing)
        |                                           |
        +-------------------+-----------------------+
                             v
                    src/mcpCore.mjs       <- shared tool metadata + request dispatch
                             |
                             v
                    src/workerPool.mjs    <- forks/reuses disposable child processes
                             |
                             v
                    src/worker.mjs (child)     <- boots CodeDotNet.Engine wasm artifact, executes one request
                             |
                             v
                    ../codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js
                        (same published artifact the browser Web Worker uses)
```

This reuses the exact same `CodeDotNet.Engine`-backed wasm artifact published by
`codedotnet.compiler` for the browser host (see `../docs/research/SPIKE_FINDINGS.md` for
the feasibility evidence). No separate build is required: run
`dotnet publish -c Release ../codedotnet.compiler/codedotnet.compiler.csproj` (or the
existing `scripts/copy-worker-assets.ps1`) before starting the sandbox server.

## Isolation and hard timeouts

Every `compile_and_run` call is executed in its own disposable Node.js **child process**
(see `src/workerPool.mjs`). This is a hard architectural requirement, not a preference:
Level 0 spike evidence (`docs/research/SPIKE_FINDINGS.md`) proved that a synchronous
infinite loop inside the wasm runtime blocks the host's event loop completely, so
`Promise.race`/`setTimeout` racing in the same process **cannot** preempt it. Only
terminating the OS process (`SIGKILL`) reliably reclaims control. The pool:

- Forks a pre-warmed pool of worker child processes (default size: 2) to absorb the
  ~1.5-2s wasm cold-start cost observed in the spike.
- Sends one `compile_and_run` request per worker at a time.
- Starts a hard-timeout timer (default: 10s, configurable per request) that
  `SIGKILL`s the worker if it has not responded in time, then replaces it with a fresh
  warm worker.
- Never reuses a worker across requests from different callers within the same process
  lifetime beyond simple pooling for latency; a worker that times out or crashes is never
  reused.

## Running

```powershell
cd codedotnet-sandbox
npm install
npm start
```

The server communicates over stdio using JSON-RPC 2.0 framed MCP-style messages
(`initialize`, `tools/list`, `tools/call`). See `src/server.mjs` for the exact method
surface. This is a deliberately minimal, dependency-free implementation (no
`@modelcontextprotocol/sdk` dependency) so it can run without network/package-registry
access; it implements the subset of the MCP stdio protocol needed for `tools/list` and
`tools/call`.

## Tools

### `compile_and_run`

Compiles and executes a single-file C# program using the curated reference surface
documented in `CodeDotNet.Engine.CompilerHost`.

Input:
```json
{
  "source": "using System;\nConsole.WriteLine(\"hi\");",
  "stdin": "",
  "timeoutMs": 10000
}
```

Output: the same `CompileAndRunResponse` JSON shape produced by the browser worker
(`status`, `diagnostics`, `output`, `outputTruncated`, `exitCode`, exception fields,
timing fields), plus `timedOut: true` if the hard timeout fired.

## Status / scope

This is a working v0.1 scaffold implementing worker-pool isolation and hard timeouts
(the two properties validated in the Level 0 spike). Not yet implemented, deferred to
follow-up work: memory/resource ceilings beyond timeout, a capability/permission model,
structured audit logging, and full MCP SDK protocol compliance (prompts/resources,
cancellation notifications, etc).

## Browser Playground (WebSocket transport)

Browsers cannot spawn stdio child processes, so a small WebSocket transport
(`src/wsServer.mjs`) exposes the exact same `initialize`/`tools/list`/`tools/call`
dispatch as the stdio server (shared via `src/mcpCore.mjs`), letting the
`codedotnet.web` React app's **MCP Playground** tab connect, discover tools, edit
source, and invoke `compile_and_run` directly from the browser.

Run both processes side by side for a live demo:

```powershell
# Terminal 1: start the MCP WebSocket server (defaults to ws://127.0.0.1:7900)
cd codedotnet-sandbox
npm install
npm run serve

# Terminal 2: start the web app
cd ../codedotnet.web
npm install
npm run dev
```

Then open the dev server URL in a browser, switch to the **MCP Playground** tab, click
**Connect** (using the default `ws://127.0.0.1:7900`, or override via the URL field),
edit the sample C# program, and click **Call compile_and_run**. The panel shows the
parsed result (status, output, diagnostics, timings) as well as the raw JSON-RPC
`tools/call` response for transparency.

Set `MCP_ALLOWED_ORIGIN` (exact origin string, or `*` to disable the check) if the web
app is served from somewhere other than `localhost`. By default the WebSocket server
accepts any origin in local development; tighten this before exposing it beyond your
machine.

## Registering with an MCP client

A machine-readable manifest is provided at `mcp.json` describing the server's stdio
transport and tool surface, for tooling that can consume it directly.

### VS Code (`mcp.json` in your user or workspace MCP configuration)

```json
{
  "servers": {
    "codedotnet-sandbox": {
      "command": "node",
      "args": ["src/server.mjs"],
      "cwd": "${workspaceFolder}/codedotnet-sandbox"
    }
  }
}
```

### Claude Desktop (`claude_desktop_config.json`)

```json
{
  "mcpServers": {
    "codedotnet-sandbox": {
      "command": "node",
      "args": ["C:\\path\\to\\codedotnet\\codedotnet-sandbox\\src\\server.mjs"]
    }
  }
}
```

Either client will discover the single `compile_and_run` tool via `tools/list` and can
invoke it with `{ "source": "...", "stdin": "...", "timeoutMs": 10000 }`.

