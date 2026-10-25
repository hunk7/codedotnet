# ADR-009: Local-first AI runtime

## Status

Proposed

## Context

CodeDotNet is a static browser playground with no backend (ADR-001, ADR-006, ADR-007). The AI initiative requires zero mandatory recurring cost and no mandatory cloud dependency. The primary target device (user-supplied, unverified) is Windows, AMD Ryzen 7 PRO 8840U (8C/16T), integrated Radeon 780M, 32 GB RAM, about 502 GB free disk.

## Decision

1. Add a new native .NET 10 service, `CodeDotNet.Ai`, as a separate process (with `CodeDotNet.Ai.Abstractions` and `CodeDotNet.Ai.Tests`). It references `CodeDotNet.Engine` and `CodeDotNet.Contracts`.
2. Inference runs locally through Ollama by default, on loopback.
3. **CPU-only inference is the baseline.** AMD GPU/Vulkan acceleration is optional and used only after detection and one-time validation on that machine, with automatic CPU fallback.
4. The existing playground and sandbox remain fully functional without the AI service.
5. The browser never talks to Ollama directly.
6. No cloud provider is part of the default configuration.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Node/TypeScript AI service | Cannot reuse `CodeDotNet.Engine` Roslyn natively; duplicates contracts |
| Extend `codedotnet-sandbox` | Mixes the untrusted-code execution boundary with a model-facing service |
| In-browser inference (WebGPU/wasm) | Weak on the target device and large downloads; not evaluated here |
| Hosted API | Violates zero-mandatory-cost and local-first goals |
| Docker-only deployment | Adds an unverified dependency on the target device |

## Trade-offs

- Another process and port to manage (mitigated by ADR-015 scripts).
- CPU inference limits model size and speed; accepted, measured through benchmarks.

## Consequences

- Privacy docs and CSP must be updated when the service ships.
- `CodeDotNet.Ai` is a native project, not a browser-wasm one.

## Validation required

- Hardware probe and benchmarks BM-01..BM-04 on the target device.
- Model license review before recommending any model.
- Check whether Ollama can use the Radeon 780M on this machine; do not assume.
