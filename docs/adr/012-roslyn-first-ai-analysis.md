# ADR-012: Roslyn-first AI analysis

## Status

Proposed

## Context

Compilers give exact, repeatable facts; models give plausible text. `CompilerHost` currently returns compiler diagnostics only. The browser runs Roslyn in wasm; `CodeDotNet.Engine` is plain `net10.0` and can run natively.

## Decision

1. For review, debug and test generation, gather deterministic evidence first (diagnostics, syntax and semantic facts) using native Roslyn in `CodeDotNet.Ai`, then give that evidence to the model.
2. Execution of generated or user code stays in the existing wasm sandbox through MCP `compile_and_run`.
3. UI labels distinguish "Roslyn diagnostic" from "AI suggestion". "Compiled" and "Tests passed" labels require a matching tool result.
4. Semantic-model and analyzer evidence is new code on top of `CodeDotNet.Engine`.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Model-only analysis | Hallucination risk, no ground truth |
| Run Roslyn analysis in the browser worker | Larger wasm payload and slower; reconsider later |
| Reuse `complexity.ts` | Heuristic only |

## Trade-offs

Native and wasm Roslyn may differ in reference sets; the shared `Basic.Reference.Assemblies.Net100` set is reused to limit drift.

## Consequences

Evidence payloads need size limits and a schema.

## Validation required

Parity tests between native and wasm diagnostics on a fixed corpus; golden review cases.
