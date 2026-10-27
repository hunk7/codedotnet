# 🏗️ Build the full local stack

Follow this guide to reproduce the whole system, including the MCP sandbox and the local AI
assistant shown in the [demo video](images/codenet.mp4), on your own machine. Windows with
PowerShell is the primary, tested path. The services themselves are cross-platform, but the helper
scripts are PowerShell.

For a condensed checklist and manual validation steps see [`ai/local-setup.md`](ai/local-setup.md).

## 1. Install prerequisites

| Tool | Version | Why |
| --- | --- | --- |
| [.NET SDK](https://dotnet.microsoft.com/download) | 10 | Builds the compiler worker and runs the AI service |
| [Node.js](https://nodejs.org/) | 22 or later | Frontend and MCP sandbox |
| [Git](https://git-scm.com/) | any | Clone the repository |
| [Ollama](https://ollama.com/download) | current | Runs the local AI models (only for the AI tab) |

Check what is missing (this installs nothing):

```powershell
scripts/dev/Install-CodeDotNetPrereqs.ps1
```

## 2. Clone and install dependencies

```powershell
git clone https://github.com/hunk7/codedotnet.git
cd codedotnet

cd codedotnet.web;      npm install; cd ..
cd codedotnet-sandbox;  npm install; cd ..
```

## 3. Build the compiler worker (one time)

This publishes the .NET 10 `browser-wasm` artifact that both the browser and the MCP sandbox use.

```powershell
cd codedotnet.web
npm run build:worker
cd ..
```

## 4. Pull the AI models (one time)

```powershell
ollama pull qwen2.5-coder:7b     # chat model, recommended for 16 GB+ RAM
ollama pull nomic-embed-text     # embeddings for the knowledge base
```

Low on memory? Use the smaller model instead: `ollama pull qwen2.5-coder:1.5b`.

If the recommended model is not installed, the service automatically falls back to another
installed chat model (for example `llama3.2`) and logs a warning. Code workflows such as test
generation work best with a coder model.

## 5. Start everything

```powershell
scripts/dev/Start-CodeDotNet.ps1
```

This starts Ollama (if needed), the sandbox (7900), the AI service (7901) and the frontend (5173),
then prints a **pairing code**. Logs are written to `.codedotnet/logs/`.

Useful options:

```powershell
scripts/dev/Start-CodeDotNet.ps1 -Profile low-resource   # force the small profile
scripts/dev/Start-CodeDotNet.ps1 -GpuMode off            # CPU only (default behaviour is auto)
scripts/dev/Start-CodeDotNet.ps1 -NoFrontend             # do not start the Vite dev server
scripts/dev/Start-CodeDotNet.ps1 -NoFrontend -AllowedOrigin https://<username>.github.io   # use the hosted site
```

## 6. Pair the AI tab

1. Open <http://localhost:5173> and go to the **AI (local)** tab.
2. Leave the service URL as `http://127.0.0.1:7901/v1`.
3. Paste the pairing code printed by the start script (or read it from `.codedotnet/logs/ai.out.log`).
4. Click **Pair**. The status changes to **Paired**.

The pairing code is **reusable for 30 minutes**. You pair once and everything works for that
window. If it expires, restart the AI service (or wait for the new code it logs) and pair again.

## 7. Verify

```powershell
scripts/dev/Test-CodeDotNetHealth.ps1   # exit code 0 = healthy
```

## 8. Stop

```powershell
scripts/dev/Stop-CodeDotNet.ps1
```

## Related

- [`deployment.md`](deployment.md): GitHub Pages deployment and using the hosted site with your local AI.
- [`ai/rag.md`](ai/rag.md): how the knowledge base and grounded answers work.
- [`ai/troubleshooting.md`](ai/troubleshooting.md): common problems.
