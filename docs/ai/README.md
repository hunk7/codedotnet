# CodeDotNet AI Documentation

Start with the [root README](../../README.md) for setup and the [demo video](../images/codenet.mp4) for a walkthrough.

Status: **Phases 0-8 implemented. The full local stack (Ollama chat, pairing, review, debug, test generation, Ask, MCP sandbox) has been run end to end on a developer machine and is covered by automated tests. Not validated: GPU mode, a live Qdrant, real browsers over HTTPS, and benchmark runs. See [`plan-validation-report.md`](plan-validation-report.md).**

| Document | Purpose | Status |
|---|---|---|
| [`codedotnet-ai-master-implementation-plan.md`](codedotnet-ai-master-implementation-plan.md) | Master plan (26 sections + Amendment 1) | Proposed |
| [`feasibility.md`](feasibility.md) | Review of the research proposal | Proposed |
| [`requirements-traceability.md`](requirements-traceability.md) | Requirements to phases, ADRs, tests | Proposed |
| [`plan-validation-report.md`](plan-validation-report.md) | What was actually checked | Current |
| [`hardware-and-resource-profiles.md`](hardware-and-resource-profiles.md) | Target device and profiles | Proposed |
| [`benchmarks.md`](benchmarks.md) | Benchmark definitions and rules | Proposed |
| [`local-setup.md`](local-setup.md) | Windows installation steps | Draft |
| [`rag.md`](rag.md) | How RAG is used, with real output | Current |
| [`operations.md`](operations.md) | Start/stop/health/backup runbook | Draft |
| [`troubleshooting.md`](troubleshooting.md) | Windows troubleshooting | Draft |
| `ai-srs.md`, `architecture.md`, `rag-design.md`, `agent-design.md`, `data-governance.md`, `threat-model.md`, `evaluation.md`, `api.md`, `model-governance.md` | Describe the implemented system | Current |

Context: [`../context/project-overview.md`](../context/project-overview.md). Decisions: [`../adr/`](../adr/README.md) 009-015.

The research proposal at `docs/research/codedotnet-ai-zero-cost-feasibility-and-engineering-plan.md` is unchanged and remains an input, not a decision.
