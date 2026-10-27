# Operations Runbook

Status: **Draft; proposed procedures.**

## Start and stop [P]

- `Start-CodeDotNet.ps1` writes `.codedotnet/run/pids.json` (PID and process start time per component).
- `Stop-CodeDotNet.ps1` stops only entries whose PID and start time still match. It never kills by name.

## Health [P]

`Test-CodeDotNetHealth.ps1` order: SQLite, Ollama, chat model, embedding model, Qdrant (optional), compiler worker assets, MCP sandbox, AI orchestrator, frontend. Exit codes: 0 healthy, 1 degraded, 2 failed.

## Backup

Back up the SQLite file under `%LOCALAPPDATA%\CodeDotNet\ai\` using a consistent backup method (not a raw copy while writing). Vectors are rebuildable from sources.

## Model updates

1. Record the current model digest.
2. Pull the new model.
3. Re-run relevant benchmarks and evaluation.
4. If the embedding model or dimension changes, create a new versioned index; never mix dimensions.

## Diagnostics

`Get-CodeDotNetDiagnostics.ps1` [P] collects versions, profile, health, and sanitized logs. No source code, prompts or secrets.

## Incident response

Disable the affected AI feature, preserve sanitized traces, rotate the pairing token, and restart.
