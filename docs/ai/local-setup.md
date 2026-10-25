# Local Setup (Windows)

Status: **Scripts exist under `scripts/dev/` (Phase 1). Verified locally without Ollama: prerequisite check, hardware probe, sandbox MCP probe, AI service health/auth/Origin. Not yet verified: end-to-end with a real Ollama model, `Start-CodeDotNet.ps1` full run, GPU mode.**

## Prerequisites

- Windows 11 or 10, PowerShell 5.1 or later.
- .NET SDK 10, Node.js 22 or later, Git.
- Ollama for Windows.
- Free disk: enough for models; check with the probe.

## Steps [P]

1. Clone the repository and stay on the working branch.
2. `scripts/dev/Install-CodeDotNetPrereqs.ps1`: verifies tools, prints what is missing; installs nothing without confirmation.
3. `scripts/dev/Invoke-HardwareProbe.ps1`: writes the probe result and the chosen profile.
4. Pull models listed by the profile (names unverified; see [`hardware-and-resource-profiles.md`](hardware-and-resource-profiles.md)).
5. Build worker assets: `npm run build:worker` in `codedotnet.web` [V command].
6. `scripts/dev/Start-CodeDotNet.ps1`: starts Ollama (if needed), sandbox, AI service, frontend; prints a pairing code.
7. Open the frontend and enter the pairing code.
8. `scripts/dev/Test-CodeDotNetHealth.ps1`: expect exit code 0.

## CPU-only

Set `gpuMode` to `off`. This is the baseline and requires nothing beyond Ollama.

## Optional Qdrant

Off by default. Enabled with `-WithQdrant` [P, not implemented yet], CPU mode only. Packaging on Windows is unverified.
