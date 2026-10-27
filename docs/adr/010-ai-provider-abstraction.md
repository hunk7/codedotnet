# ADR-010: AI provider abstraction

## Status

Proposed

## Context

The default runtime is Ollama, but models and runtimes change quickly. Business logic must not depend on a single vendor API.

## Decision

Define interfaces in `CodeDotNet.Ai.Abstractions`: a chat/generation provider with streaming and cancellation, an embedding provider, and a health/capability probe. `OllamaProvider` is the first implementation. A fake provider is required for tests. Every Ollama request sets `num_ctx`, `num_gpu`, `keep_alive` and `num_predict` explicitly. Provider choice is configuration; non-loopback providers are disabled unless the user opts in.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| Call Ollama directly everywhere | Couples logic to one API; hard to test |
| Adopt a large third-party AI framework | Extra dependency and churn; not evaluated |
| OpenAI-compatible HTTP only | May be added as a second adapter; not sufficient alone for model/digest metadata |

## Trade-offs

A thin abstraction may leak provider-specific options; capability flags are used instead of lowest-common-denominator behavior.

## Consequences

Contract tests run the same cases against fake and Ollama providers.

## Validation required

Contract test suite; verify the Ollama option names and `/api` endpoints against the installed Ollama version.
