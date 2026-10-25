# Hardware and Resource Profiles

Status: Proposed. All numbers below are **provisional configuration values**, not measurements.

## 1. Target device [A]

User-supplied, not verified by the repository: Windows; AMD Ryzen 7 PRO 8840U (3.30 GHz, 8C/16T); integrated AMD Radeon 780M; 32 GB RAM at 5600 MT/s; 954 GB storage with about 502 GB available.

`Invoke-HardwareProbe.ps1` [P] must record actual values (OS build, cores, RAM, GPU name and driver, free disk) before any profile is resolved.

## 2. Principles

1. CPU-only inference is the supported baseline.
2. The integrated GPU shares system memory (UMA). Offloading may speed up compute but does not add memory [A].
3. One resident chat model plus one embedding model. `OLLAMA_MAX_LOADED_MODELS=2`, `OLLAMA_NUM_PARALLEL=1` [A: verify variable names against installed Ollama].
4. Every request sets `num_ctx`, `num_gpu`, `keep_alive`, `num_predict`.
5. Leave RAM for the OS, browser, IDE and sandbox. The memory budget is set from BM-02 results, not guessed.

## 3. Profiles

| Setting | `low-resource` | `recommended` |
|---|---|---|
| Chat model class | 1B-3B | 3B-7B Q4 |
| Example chat model [A] | `qwen2.5-coder:1.5b` | `qwen2.5-coder:7b` (Q4_K_M), fallback 3-4B |
| Embedding model [A] | `nomic-embed-text` | `nomic-embed-text` |
| `num_ctx` | 8K | 8K default, 16K cap after benchmark |
| Final chunks | 3 | 5 (range 4-6) |
| Candidates lexical + vector | 10 + 10 | 20 + 20 |
| Agent iterations default / max | 3 / 4 | 4 / 6 |
| Sandbox pool size | 1 | 2 |
| Embedding batch | 4-8 | 8-16, paused during generation |
| LLM reranker | off | off by default |
| `keep_alive` | short | moderate |

Model names and licenses are from memory and must be confirmed (A-04). The profile is chosen by `IProfileResolver` from the probe result and user overrides. Users can force a profile.

## 4. GPU policy

`gpuMode`: `off`, `auto`, `on`.

- `off`: send `num_gpu: 0`.
- `auto` (default): detect the AMD GPU; run a one-time validation (load a small model, short generation, compare output sanity and speed with CPU); cache result keyed by GPU driver version, Ollama version and model digest; on any failure or slowdown use CPU.
- `on`: attempt GPU; if validation fails, warn and fall back unless the user sets a strict flag.

Not assumed: Vulkan or ROCm support for the 780M under Ollama on Windows (A-03).

## 5. Disk and data

User data in `%LOCALAPPDATA%\CodeDotNet\ai\`. Model weights are managed by Ollama. Disk budget is measured by BM-11. The health script warns when free disk is below a configured floor.

## 6. Acceptance criteria

| ID | Criterion |
|---|---|
| AC-HW-001 | Hardware probe runs on the target device and emits schema-valid JSON |
| AC-HW-002 | Profile resolves deterministically from probe and overrides |
| AC-HW-003 | CPU-only mode passes the smoke workflow with `gpuMode=off` |
| AC-HW-004 | GPU `auto` falls back to CPU on validation failure without user action |
| AC-HW-005 | No more than one chat and one embedding model resident |
| AC-HW-006 | Playground stays responsive during generation (BM-12) |
| AC-HW-007 | Start/stop/health scripts work from a clean checkout; stop touches only recorded processes |
| AC-HW-008 | Every benchmark number in docs links to a schema-valid result file |

## 7. Requirement IDs

NFR-HW-001..012 and FR-OPS-001..005 are mapped in [`requirements-traceability.md`](requirements-traceability.md).
