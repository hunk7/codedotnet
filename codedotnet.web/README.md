# 🖥️ codedotnet.web

The React + TypeScript + Vite frontend for **codedotnet**. It hosts three tabs:

| Tab                | Needs                                                    | What it does                                                                                                                               |
| ------------------ | -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| **Editor**         | Nothing (runs entirely in the browser)                   | Monaco C# editor; compiles and runs code in a Web Worker using Roslyn and the .NET 10 WebAssembly runtime.                                 |
| **MCP Playground** | `codedotnet-sandbox` on `ws://127.0.0.1:7900`            | Browser client for the Model Context Protocol server: discovers tools, calls `compile_and_run`, shows the parsed and raw JSON-RPC results. |
| **AI (local)**     | `CodeDotNet.Ai` on `http://127.0.0.1:7901/v1` and Ollama | Pair with the local AI service, then review, debug, generate tests and ask questions about your code.                                      |

🎬 See the [demo](../docs/images/demo.gif) and the [root README](../README.md) for the
full walkthrough and the complete local setup. Architecture, testing, deployment, security and
privacy docs live in [`docs/`](../docs/).

## 🚦 Local development

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Node.js 22+](https://nodejs.org/).

```powershell
npm install

# Publish the compiler worker once so the Editor tab can load it
npm run build:worker

npm run dev
```

Open the printed URL (normally <http://localhost:5173>).

### Editor only

The commands above are all you need. The MCP and AI tabs show a disconnected/unpaired state
until their services are running.

### Full stack

From the repository root, start everything (sandbox, AI service, frontend) with:

```powershell
scripts/dev/Start-CodeDotNet.ps1
```

Then open the **AI (local)** tab and paste the pairing code the script prints. The code is
reusable for 30 minutes. Details, model installation and troubleshooting are in the
[full local stack guide](../docs/local-stack.md).

> **Note:** The hosted GitHub Pages site contains all three tabs, but only the Editor works on its
> own. MCP and AI need the local services; start them with
> `scripts/dev/Start-CodeDotNet.ps1 -NoFrontend -AllowedOrigin https://<username>.github.io`.
> See [GitHub Pages deployment](../docs/deployment.md#hosted-site-and-the-local-ai-tab).

## 🧠 Shared state between tabs

The Editor and MCP Playground share the same source code. The AI tab works on that code and on
the result of the **last run**, whichever tab produced it. The AI tab shows a banner such as
`Editor code (18 lines) · last run (MCP): BuildFailed` so you can see what it will analyze.
The AI bearer token is kept in memory only and is never written to storage.

## 📜 Common scripts

| Script                                          | Purpose                                                                                   |
| ----------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `npm run dev`                                   | Start the Vite dev server.                                                                |
| `npm run build`                                 | Type-check and build for production.                                                      |
| `npm run preview`                               | Serve the production build locally.                                                       |
| `npm run typecheck`                             | Type-check without emitting.                                                              |
| `npm run lint`                                  | Run ESLint.                                                                               |
| `npm run format` / `format:check`               | Format with Prettier / verify formatting.                                                 |
| `npm test`                                      | Run the Vitest suite once.                                                                |
| `npm run build:worker` / `build:worker:release` | Publish the `codedotnet.compiler` worker and copy its assets into `public/dotnet-worker`. |

See [`docs/testing.md`](../docs/testing.md) and [`docs/deployment.md`](../docs/deployment.md) for details.
