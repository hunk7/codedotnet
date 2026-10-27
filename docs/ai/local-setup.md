# Local Setup (Windows)

See the [full local stack guide](../local-stack.md) for the shortest path and the [demo video](../images/codenet.mp4) for a walkthrough of the running stack.

Status: **Scripts exist under `scripts/dev/`. The full stack (sandbox, AI service, frontend, Ollama, pairing, review, debug, test generation and Ask) has been run end to end locally. Not yet verified: GPU mode, a live Qdrant and published benchmarks.**

## Prerequisites

- Windows 11 or 10, PowerShell 5.1 or later.
- .NET SDK 10, Node.js 22 or later, Git.
- Ollama for Windows.
- Free disk: enough for models; check with the probe.

## Steps [P]

1. Clone the repository and stay on the working branch.
2. `scripts/dev/Install-CodeDotNetPrereqs.ps1`: verifies tools, prints what is missing; installs nothing without confirmation.
3. `scripts/dev/Invoke-HardwareProbe.ps1`: writes the probe result and the chosen profile.
4. Pull the models: `ollama pull qwen2.5-coder:7b` (or `qwen2.5-coder:1.5b` on small machines) and `ollama pull nomic-embed-text`. If the profile's chat model is missing, the service falls back to another installed chat model and logs a warning; see [`hardware-and-resource-profiles.md`](hardware-and-resource-profiles.md).
5. Build worker assets: `npm run build:worker` in `codedotnet.web` [V command].
6. `scripts/dev/Start-CodeDotNet.ps1`: starts Ollama (if needed), sandbox, AI service, frontend; prints a pairing code.
7. Open the frontend and enter the pairing code.
8. `scripts/dev/Test-CodeDotNetHealth.ps1`: expect exit code 0.
9. `scripts/dev/Test-CodeDotNetSmoke.ps1`: pairing, auth, Origin, knowledge-base round trip. Uses the pairing code from `.codedotnet/logs/ai.out.log`, which stays valid for 30 minutes (restart the stack if it has expired). Ollama-dependent checks print `[SKIP]` when Ollama is not running.

## Manual validation

1. **Build and tests:** `dotnet build codedotnet.slnx -c Release`; `dotnet test CodeDotNet.Engine.Tests -c Release`; `dotnet test CodeDotNet.Ai.Tests -c Release`; `npm test` in `codedotnet-sandbox`; `npm test`, `npm run typecheck`, `npm run lint`, `npm run build` in `codedotnet.web`.
2. **Start:** `scripts/dev/Start-CodeDotNet.ps1`. Note the pairing code it prints.
3. **Playground without AI:** open http://localhost:5173, run the default C# program, and confirm output appears. This must work even if the AI service is stopped.
4. **MCP tab:** open the MCP tab, click **Connect**, and run a snippet through the sandbox.
5. **AI tab:** open the AI (local) tab, paste the pairing code, click **Pair**, then click **Check service** (expect `ok`, or `degraded` if the embedding model is not pulled).
6. **With Ollama**
7. **Security:** from PowerShell, `Invoke-RestMethod http://127.0.0.1:7901/v1/kb/documents` returns 401. Sending `-Headers @{Origin='https://evil.example'}` returns 403. `netstat -ano | findstr 7901` shows only `127.0.0.1`.
8. **Stop:** `scripts/dev/Stop-CodeDotNet.ps1`; ports 7900, 7901 and 5173 are free again.
9. **Packaging:** `scripts/dev/Publish-CodeDotNetAi.ps1`, then `dotnet artifacts/ai/CodeDotNet.Ai.dll`.

## CPU-only

Set `gpuMode` to `off`. This is the baseline and requires nothing beyond Ollama.

## Optional Qdrant

Off by default. Enable with `Ai:VectorStore=qdrant` plus `Ai:QdrantUrl` (loopback only; default `http://127.0.0.1:6333`) and `Ai:QdrantCollection`. Start Qdrant yourself (for example with Docker, bound to 127.0.0.1). `Start-CodeDotNet.ps1` does not start it. If Qdrant is unreachable, retrieval falls back to lexical search and `/v1/health` reports `degraded`. Verified only against an in-memory fake; not against a real Qdrant.

## Packaging

`scripts/dev/Publish-CodeDotNetAi.ps1` publishes the service to `artifacts/ai` with a `SHA256SUMS.txt`. Run it with `dotnet artifacts/ai/CodeDotNet.Ai.dll`. A smoke run of that build confirmed loopback binding, 401 without a token and an honest `unavailable` health when Ollama is absent.
