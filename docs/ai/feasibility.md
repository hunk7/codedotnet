# Feasibility Review

Reviews `docs/research/codedotnet-ai-zero-cost-feasibility-and-engineering-plan.md` against the repository. Verdict: **feasible with changes**.

## Classification

Categories: Compatible, Modified, New, Conflict, Postpone, Reject.

| Proposal element | Class | Notes |
|---|---|---|
| Local Ollama runtime | Compatible | Default provider; model names unverified |
| Provider abstraction | Compatible | ADR-010 |
| Separate AI service | Modified | Native .NET 10 `CodeDotNet.Ai`, not under `codedotnet.compiler` |
| Qdrant plus SQLite as baseline | Conflict | Memory and operations cost on one laptop; SQLite FTS5 + sqlite-vec default; Qdrant optional CPU adapter (ADR-011) |
| Roslyn-first analysis | Modified | Native Roslyn via `CodeDotNet.Engine`; Engine currently gives diagnostics only (ADR-012) |
| Paths `codedotnet.compiler.tests`, Roslyn inside `codedotnet.compiler` | Conflict | Actual: `CodeDotNet.Engine/`, `CodeDotNet.Engine.Tests/` |
| Separate stdout/stderr | Conflict | Merged today in `ExecutionHost`; change is a separate decision |
| HTTP endpoint from the browser | Conflict | CSP lacks `http://` loopback; HTTPS-to-HTTP-loopback behavior unverified |
| Extensible MCP tools | Modified | `mcpCore.mjs` hard-codes one tool; registry needed |
| `/v1/ai/...` endpoints | Modified | Plan uses `/v1/...` on a separate port |
| Bounded agent state machine | Compatible | ADR-013 |
| Observability (OpenTelemetry) | Modified | Local only, no raw content (ADR-014) |
| Golden dataset and RAG metrics | Compatible | Targets treated as proposals, thresholds unset until measured |
| Suggested release thresholds (for example Recall@10 >= 0.85) | Postpone | Not adopted without data on this hardware |
| PDF/DOCX ingestion | Postpone | After v0.1 |
| Many UI modes | Postpone | Staged by phase |
| Cloud providers | Reject (default) | Opt-in only, off by default |
| Reranker LLM | Postpone | Off by default; BM-09 first |
| Multi-agent with several models | Reject | One resident model on target device |

## Hardware impact

The proposal does not address the target device. See [`hardware-and-resource-profiles.md`](hardware-and-resource-profiles.md).

## Open verification items

Browser loopback behavior, sqlite-vec on Windows, Ollama options and GPU support, model licenses, Qdrant Windows packaging. See [`../context/assumptions-and-open-questions.md`](../context/assumptions-and-open-questions.md).
