# 📚 RAG in codedotnet

RAG (retrieval-augmented generation) is how the **Ask** action answers from documents you add,
instead of from the model's memory. Everything runs on your machine: documents are stored in a local
SQLite database, embeddings come from Ollama (`nomic-embed-text`), and the answer comes from your
local chat model. Nothing is sent to a cloud service.

The goal is **grounded, checkable answers**: every claim points to a numbered source, the service
validates those numbers, and when nothing relevant is found it says so instead of guessing.

For the design reference see [`rag-design.md`](rag-design.md). For routes see [`api.md`](api.md).

## Where RAG is used

| Place | Behavior |
| --- | --- |
| **AI (local) tab → Ask**, with **Answer from my knowledge base (with citations)** ticked | Retrieves evidence, answers only from it, returns citations. |
| `POST /v1/ask` with `useKnowledge: true` (the default) | Same as above for API callers. |
| `POST /v1/ask` with `useKnowledge: false`, or an empty knowledge base | Plain, **ungrounded** answer. The result is flagged `grounded: false`. |

Review, Debug and Generate tests do not use the knowledge base. They work from your code and the
real run output.

## The pipeline

```text
 Add document ──▶ chunk ──▶ SQLite (text + FTS5 keyword index)
  POST /kb/documents          └──▶ embed (Ollama) ──▶ vector store (SQLite, optional Qdrant)

 Question ──▶ keyword search (FTS5) ┐
          └─▶ vector search (cosine) ┴─▶ rank fusion ──▶ top chunks
                                                          │
                       no relevant chunk? ──▶ "I could not find evidence…" (model is NOT called)
                                                          ▼
                              numbered evidence + question ──▶ local chat model
                                                          ▼
                       streamed tokens ──▶ citations validated against the evidence ──▶ done
```

1. **Ingest.** `POST /v1/kb/documents` takes `{ title, source, text }` (text up to 200,000
   characters). The text is split by a structure-aware Markdown chunker: headings define sections
   and become each chunk's heading path, code fences are kept whole where possible, and chunks are
   capped at about 1,200 characters.
2. **Index.** Chunks are stored in SQLite with a full-text (FTS5) index. Each chunk is also
   embedded with `nomic-embed-text` and written to the vector store. The store records the
   embedding model and dimension, and refuses a different model until the index is rebuilt.
3. **Retrieve.** The question is searched by keyword and by vector. The two result lists are merged
   with rank fusion. A chunk must have keyword support **or** clear a minimum vector similarity
   (`Ai:MinVectorScore`, 0.5), otherwise nearest-neighbour search would return unrelated text for
   every question and "no evidence" could never happen.
4. **Ground.** The top chunks are numbered `[1]`, `[2]`… and sent to the model as *untrusted
   reference text*. The system prompt tells the model to answer only from them, to cite them, and
   to ignore any instructions that appear inside them.
5. **Validate.** After the answer is streamed, the service checks every `[n]` the model wrote. Only
   numbers that match a passage that was actually supplied become `citation` events. Invented
   numbers are reported in `invalidCitations`.
6. **Fall back.** If embeddings or the vector store are unavailable, retrieval continues with
   keyword search only and says so in `note`. Chat, review, debug and tests are not affected.

## What it outputs

The `/v1/ask` response is a Server-Sent Events stream. The examples below are real output from a
local run (model `llama3.2`) with `docs/ai/rag-design.md` and `docs/ai/troubleshooting.md` added to
the knowledge base. Token events are shortened here: the real stream sends one small `token` event
per word.

### 1. A question the knowledge base can answer

Question: *What happens when the vector store or embeddings fail during retrieval?*

```text
event: token
data: {"text":"According"}

event: token
data: {"text":" to"}

  … one token event per word …

event: citation
data: {"label":1,"chunkId":"d-f195fccca22d-0","source":"docs/ai/rag-design.md","title":"rag-design.md","heading":"RAG Design"}

event: done
data: {"outcome":"ok","stopReason":"complete","grounded":true,"citations":1,"invalidCitations":[],"usedVectors":true,"note":null}
```

The tokens concatenate to:

> According to the evidence passage [1], when the vector store or embeddings fail during retrieval,
> the service continues lexical-only retrieval.

What to read from it:

- `citation` lists the passage behind `[1]`: its source file, title and heading path.
- `grounded: true` and `citations: 1` mean the answer was backed by supplied evidence.
- `usedVectors: true` means semantic search contributed. If embeddings are unavailable it is
  `false` and `note` explains why.
- `invalidCitations: []` means the model did not cite anything that was not provided.

### 2. A question the knowledge base cannot answer

Question: *How do I bake a sourdough loaf?*

```text
event: token
data: {"text":"I could not find evidence for this in your knowledge base, so I will not guess."}

event: done
data: {"outcome":"ok","stopReason":"insufficient_evidence","grounded":false,"note":null}
```

No relevant chunk was found, so the chat model was **not called at all**. The service answers
directly with a fixed message and `stopReason: "insufficient_evidence"`.

### In the AI tab

With **Answer from my knowledge base (with citations)** ticked, the answer streams into the result
panel as plain text, followed by one line per cited source:

```text
[1] rag-design.md › RAG Design — docs/ai/rag-design.md
```

When nothing relevant is found the panel shows the fixed reply and the line
*Not enough evidence in the knowledge base to answer.* Any `note` (for example that vector search was
unavailable) is shown under the answer.

The AI tab has no document-upload control yet. Documents are added through the API, as shown below.

## Try it yourself

1. Pair the AI tab (see [`../local-stack.md`](../local-stack.md)).
2. Add a document. From PowerShell, with the token you receive from `POST /v1/pair`:

   ```powershell
   $body = @{ title = 'notes.md'; source = 'notes.md'; text = [IO.File]::ReadAllText("$PWD\notes.md") } | ConvertTo-Json
   Invoke-RestMethod -Method Post http://127.0.0.1:7901/v1/kb/documents `
     -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' -Body $body
   # documentId, chunks, embedded, note
   ```

   A successful ingest returns `201` with `{ "documentId": "d-…", "chunks": 2, "embedded": true, "note": null }`.
   If `embedded` is `false`, the document was stored for keyword search only and `note` says why
   (usually the embedding model is not pulled: `ollama pull nomic-embed-text`).
3. List what is stored with `GET /v1/kb/documents`, which returns `{ documents, chunks }`.
4. Ask a question in the AI tab, or `POST /v1/ask` with `{ "question": "…", "useKnowledge": true }`.
5. Remove a document with `DELETE /v1/kb/documents/{id}`.

## Limits and honesty notes

- Documents: up to 200,000 characters each; title up to 200 characters; source up to 500.
- Questions: up to 8,000 characters.
- Evidence sent to the model is trimmed to fit the model's context window, keeping the best chunks.
- Citation checking proves the cited passage was **supplied**, not that the model paraphrased it
  correctly. Read the cited source for anything important.
- Small models can ignore the "cite your sources" instruction. Then the answer has
  `grounded: false` even though evidence was retrieved.
- Defaults such as `MinVectorScore` are unbenchmarked. See [`evaluation.md`](evaluation.md).
- Optional Qdrant support exists (loopback only) but has not been run against a live Qdrant.
