# Data Governance

Status: **Describes implemented behavior; retention policy is a proposal for the maintainer to confirm.**

- **Local only.** Prompts, source code and knowledge-base text go to the local Ollama (and optionally a local Qdrant). The service makes no other outbound calls.
- **Where data lives.** `Ai:DataDirectory` (default `%LOCALAPPDATA%\CodeDotNet\ai`) holds the SQLite knowledge base and vectors. Qdrant keeps its own storage if enabled.
- **Browser.** The pairing token is memory-only. The AI panel does not persist questions or answers.
- **Deletion.** `DELETE /kb/documents/{id}` removes a document. Deleting the data directory removes everything else.
- **Backup.** Copy the data directory while the service is stopped; see [`operations.md`](operations.md).
