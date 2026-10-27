# Model Governance

Status: **Draft; model names and licenses are unverified.**

- Models are pulled by the user into a local Ollama. Nothing is bundled or downloaded by the service.
- Default names come from the resource profile and can be overridden with `Ai:ChatModel` and `Ai:EmbeddingModel`. Check exact tags and licenses before use.
- `/v1/health` reports `missing` when a configured model is not pulled.
- Changing the embedding model requires rebuilding the vector index (enforced by the index manifest).
- No model is evaluated or endorsed here; see [`evaluation.md`](evaluation.md) and [`benchmarks.md`](benchmarks.md).
