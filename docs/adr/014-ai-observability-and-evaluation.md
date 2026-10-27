# ADR-014: AI observability and evaluation

## Status

Proposed

## Context

ADR-007 forbids analytics in version 1. AI quality needs measurement, but source code and prompts are sensitive.

## Decision

1. Telemetry is local only; nothing is sent off the machine.
2. Use OpenTelemetry-style traces and metrics with attributes limited to feature, prompt id/version, model id/digest, counts, durations and outcome. No raw source, prompts or secrets.
3. Evaluation uses a versioned golden dataset, deterministic checks first (citations, compile/test results), and an optional local judge model that is never the sole gate.
4. Benchmark results come only from a harness and must validate against a committed JSON schema. No hand-written numbers.
5. `docs/ai/benchmarks/thresholds.json` holds null limits until measured; a release gate fails while any threshold is null.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| No evaluation | Cannot detect regressions |
| Cloud observability | Violates local-first and ADR-007 |
| LLM judge only | Not repeatable |

## Trade-offs

Local-only telemetry limits fleet-wide insight; accepted.

## Consequences

Privacy docs describe local diagnostics. A UI control to disable persistence is needed.

## Validation required

Redaction tests; schema validation of every result file.
