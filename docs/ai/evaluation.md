# Evaluation

- Owner: Maintainer (TBD)
- Status: Harness implemented (Phase 6). Thresholds and the full dataset are not.
- Seed: master plan sections 17 and 18, [`benchmarks.md`](benchmarks.md), ADR-014.

## What exists

| Piece | Location | Notes |
|---|---|---|
| Metrics | `CodeDotNet.Ai/Evaluation/Metrics.cs` | Recall@k, MRR, citation precision, median/min/max/p95. Deterministic, no model. |
| Golden dataset | `benchmarks/golden-retrieval.json` | **6 seed cases, 4 documents.** The 150-case target is not met. It exercises the harness; it says nothing about retrieval quality. |
| Retrieval evaluator | `GoldenDataset.cs` | Ingests the documents, runs each query repeatedly, records recall, MRR and latency. |
| Result writer/validator | `BenchmarkResults.cs` | Validates against `benchmarks/result.schema.json` before writing. Invalid results are refused. |
| Release gate | `ThresholdGate.cs` | Fails on any `null` limit, missing result, unknown limit name, invalid result file, or breached median. |

## Commands

```powershell
dotnet run --project CodeDotNet.Ai -c Release -- gate   # exit 1 until every limit is set and measured
dotnet run --project CodeDotNet.Ai -c Release -- eval   # prints recall/MRR/latency for the seed dataset
```

`eval` only prints. It does not write result files, because a committed result must carry machine, OS build, Ollama version and model digests from a real benchmark run. With Ollama unreachable, retrieval is lexical-only; the output reports how many queries used vectors.

## Not done

- Thresholds in `benchmarks/thresholds.json` remain `null`; the gate fails by design.
- No benchmark results exist (BM-01 to BM-12). Nothing was measured on the target machine.
- Groundedness, patch compile/test rate and agent success metrics are not in the harness yet.
- Per-profile runs on real models need a manual or self-hosted run [Q-07].
