# Agent Design

Status: **Implemented (Phase 5).** Not exercised against a real model.

- One run at a time (`409 agent_busy` otherwise). Runs can be stopped with `POST /agent/runs/{id}/stop`.
- Budgets come from the active resource profile and are checked in `AgentRunService.Validate`.
- Tools are limited to the service's own: Roslyn compile for diagnostics and the sandbox for execution. There is no file-system or shell tool.
- Progress is streamed as SSE events; the final `done` event carries the outcome.

Safety relies on the sandbox boundary and on not exposing arbitrary tools. See [`threat-model.md`](threat-model.md).
