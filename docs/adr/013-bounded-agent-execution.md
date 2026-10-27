# ADR-013: Bounded agent execution

## Status

Proposed

## Context

Agent loops can run indefinitely, call dangerous tools, or claim unverified success.

## Decision

1. Implement one explicit state machine with logical roles (Planner, Coder, Test designer, Executor, Reviewer, Fixer). Roles are prompts and states, not separate processes, and share one resident chat model.
2. Hard budgets: iterations (default 3 or 4, maximum 4 to 6 by profile), wall-clock time, tool calls, output size. User can stop at any time.
3. Typed tool allowlist. No shell, arbitrary network or arbitrary filesystem access.
4. Repeated-state detection stops loops.
5. A claim such as "tests passed" must be backed by a tool result in the same trace.
6. Patches are applied only after user selection.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Autonomous multi-agent framework | Unbounded behavior; memory cost of several models |
| Single-shot only | Cannot validate by compiling |

## Trade-offs

Fewer iterations reduce capability; defaults are provisional until benchmark BM-10.

## Consequences

Sandbox hardening (timeout cap, payload cap, cancellation) must land before agent tools call it.

## Validation required

Budget, cancellation and invalid-tool-call tests; BM-10 agent completion.
