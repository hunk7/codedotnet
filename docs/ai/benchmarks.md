# Benchmarks

Status: Proposed. **This PR contains no benchmark results.** Numbers may only enter the repository as harness-produced result files that validate against [`benchmarks/result.schema.json`](benchmarks/result.schema.json).

## Rules

1. Results come from a harness, never typed by hand.
2. Each result records machine, OS build, Ollama version, model id and digest, profile, `gpuMode`, git commit, and timestamp.
3. Documentation may quote a number only with a link to its result file.
4. `benchmarks/thresholds.json` has `null` limits. A release gate must fail while any limit is `null`.
5. Warm and cold runs are reported separately; repeat counts are recorded.

## Benchmark list

| ID | Measures | Method sketch |
|---|---|---|
| BM-01 | Model load time | Cold start to ready, per model |
| BM-02 | Idle and peak RAM | Process working set during idle and generation |
| BM-03 | Time to first token | Fixed prompts per context size |
| BM-04 | Tokens per second | Fixed generation length |
| BM-05 | `num_thread` sweep | Vary thread count; find the best value on 8C/16T |
| BM-06 | Embedding throughput | Chunks per second at batch sizes |
| BM-07 | Retrieval latency | Query to top-k, FTS5, vector and fused |
| BM-08 | Vector store comparison | sqlite-vec vs optional Qdrant CPU: latency, RAM, recall |
| BM-09 | Rerank latency | Only if a reranker is proposed |
| BM-10 | Agent completion | Success rate and iterations on a fixed task set |
| BM-11 | Disk use | Models, index, logs |
| BM-12 | Browser responsiveness | UI frame/interaction latency during generation |

## Result files

Location `docs/ai/benchmarks/results/`. See its README. Naming `<UTC date>-<benchmarkId>-<profile>.json`.
