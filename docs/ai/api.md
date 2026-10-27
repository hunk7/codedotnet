# Local AI Service API

Status: **Implemented (Phases 1-5, 7).** Source: `CodeDotNet.Ai/AiHost.cs`. Verified by the `CodeDotNet.Ai.Tests` suite and a smoke run of the published build. Not verified against a real Ollama model.

Base URL: `http://127.0.0.1:7901/v1` (loopback only; configurable with `Ai:Host` and `Ai:Port`). Request bodies are capped at 256 KiB.

## Authentication

(valid 30 minutes and reusable until then; a fresh code is printed after it expires or is locked by too many wrong attempts)

## Routes

| Method and path | Purpose |
|---|---|
| `GET /health` | `service`, `ollama`, `chatModel`, `embeddingModel` and, when configured, `qdrant` checks. Overall `ok`, `degraded` or `unavailable`. |
| `POST /pair` | Body `{ "code": "..." }`. Returns `{ token, expiresAt }`; 401 on a wrong code, 403 when the code expired or is locked. |
| `GET /status`, `/profile`, `/capacity` | Resolved profile, GPU mode, provider reachability, host capacity. |
| `POST /ask` | Body `{ question, useKnowledge }`. SSE stream. |
| `POST /kb/documents`, `GET /kb/documents`, `DELETE /kb/documents/{id}` | Ingest, list and delete knowledge-base documents. |
| `POST /review`, `/debug`, `/tests` | Code workflows (SSE). |
| `POST /agent/runs`, `GET /agent/runs/{id}/events`, `POST /agent/runs/{id}/stop` | One agent run at a time (409 `agent_busy` otherwise). |

## SSE events

`token` (`{text}`), `citation` (`{label, chunkId, source, title, heading}`), `done` (`{...}`), `error` (`{code, message}`). Clients must ignore unknown events.

## Errors

`{ "error": { "code": "...", "message": "..." } }`. Codes include `invalid_request`, `question_too_long` (413), `document_too_large` (413), `not_found`, `pairing_failed`, `pairing_code_expired`, `agent_busy`.
