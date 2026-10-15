# 🖥️ codedotnet.web

The React + TypeScript + Vite frontend for **codedotnet**, a browser-based C# playground. See the
root [README](../README.md) and the [`docs/`](../docs/) folder for architecture, testing,
deployment, security, and privacy documentation.

## 🔌 MCP Playground tab

In addition to the main **Editor** view, the app includes an **MCP Playground** tab
(`src/mcp/McpPlayground.tsx`) — a browser client for the [Model Context Protocol](https://modelcontextprotocol.io/)
server in [`codedotnet-sandbox/`](../codedotnet-sandbox/). It connects over WebSocket, discovers
tools via `tools/list`, lets you edit a C# sample, and invokes `compile_and_run` via `tools/call`,
rendering both the parsed result and the raw JSON-RPC response. See the root README's
[MCP Playground section](../README.md#-mcp-playground) and
[`codedotnet-sandbox/README.md`](../codedotnet-sandbox/README.md) for how to run the server.

## 🚦 Local development

```powershell
npm install
npm run dev
```

## 📜 Common scripts

| Script                                          | Purpose                                                 |
| ----------------------------------------------- | ------------------------------------------------------- |
| `npm run dev`                                   | Start the Vite dev server.                              |
| `npm run build`                                 | Type-check and build for production.                    |
| `npm run typecheck`                             | Type-check without emitting.                            |
| `npm run lint`                                  | Run ESLint.                                             |
| `npm test` / `test:watch`                       | Run the Vitest suite once / in watch mode.              |
| `npm run build:worker` / `build:worker:release` | Publish the codedotnet.compiler worker and copy assets. |

See [`docs/testing.md`](../docs/testing.md) and [`docs/deployment.md`](../docs/deployment.md) for details.
