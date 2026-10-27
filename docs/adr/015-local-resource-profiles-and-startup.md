# ADR-015: Local resource profiles and startup

## Status

Proposed

## Context

The target device has 8 cores/16 threads, a shared-memory integrated GPU and 32 GB RAM. The OS, browser, IDE and .NET host compete for that RAM. Model choices must be bounded and observable.

## Decision

1. Named profiles: `low-resource` and `recommended`, plus explicit overrides. Profile values are provisional configuration, not benchmark results.
2. Hold one resident chat model and one embedding model: `OLLAMA_MAX_LOADED_MODELS=2`, `OLLAMA_NUM_PARALLEL=1`.
3. `gpuMode` is `off`, `auto` or `on`. Default `auto`: detect, validate once, cache by driver + Ollama version + model digest, fall back to CPU on any failure. `num_gpu: 0` forces CPU.
4. Windows PowerShell scripts under `scripts/dev/`: `Start-CodeDotNet.ps1`, `Stop-CodeDotNet.ps1`, `Test-CodeDotNetHealth.ps1` (exit 0 healthy, 1 degraded, 2 failed), `Invoke-HardwareProbe.ps1`, `Get-CodeDotNetDiagnostics.ps1`, `Install-CodeDotNetPrereqs.ps1`.
5. Stop only processes the scripts started, recorded in `.codedotnet/run/pids.json` with start time to avoid PID reuse.
6. User data lives in `%LOCALAPPDATA%\CodeDotNet\ai\`.
7. Health check order: SQLite, Ollama, chat model, embedding model, Qdrant (optional), compiler worker assets, MCP sandbox, AI orchestrator, frontend.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Single fixed configuration | Cannot adapt to weaker machines |
| Auto-tune at runtime | Hard to explain; adds risk |
| Windows service installer | Heavy for a developer tool; revisit later |

## Trade-offs

Scripts add maintenance and need Windows testing.

## Consequences

Docs cover Windows setup and troubleshooting. Scripts are proposed here, not written.

## Validation required

Benchmarks BM-01..BM-12; script tests including a dry run; check that `num_thread` sweeping (BM-05) is justified by data.
