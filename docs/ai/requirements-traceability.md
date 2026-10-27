# Requirements Traceability

Status: Phases 0-8 are implemented. "Phase" refers to the master plan section 25. Verification rows name the planned check; the automated ones exist in `CodeDotNet.Ai.Tests`, `codedotnet.web/src/ai/__tests__` and the sandbox tests. Items needing a real Ollama, live Qdrant, GPU, real browsers or benchmark runs (AC-HW-*, BM-*, NFR-PERF-001 thresholds) are **not verified**. FR-KB-002 (Qdrant) is verified against an in-memory fake only.

| ID | Requirement | Phase | ADR | Planned verification |
|---|---|---|---|---|
| NFR-COMPAT-001 | Playground works with no AI service | all | 009 | Existing frontend and Engine tests; no-AI smoke |
| NFR-SEC-001 | AI service binds loopback only | 1 | 009 | Unit test on host validation |
| NFR-SEC-002 | Pairing token and strict Origin allowlist; CORS deny | 1 | 009 | Security regression tests |
| NFR-SEC-003 | Sandbox denies unknown Origins by default | 0 | 013 | `wsServer` test; non-browser clients without Origin still work |
| NFR-SEC-004 | Sandbox timeout and payload caps | 0 | 013 | Sandbox tests |
| NFR-PRV-001 | No raw source/prompts/secrets in logs | 1 | 014 | Redaction tests |
| NFR-PRV-002 | Local data inventory, export, delete | 3 | 014 | UI and API tests |
| FR-ASK-001 | Grounded answers with validated citations | 3 | 011 | Golden dataset, citation tests |
| FR-REV-001 | Review uses Roslyn evidence first | 2 | 012 | Parity and golden tests |
| FR-DBG-001 | Debug using run output and diagnostics | 2 | 012 | Golden tests |
| FR-TST-001 | Generated tests compiled and run through sandbox | 4 | 012, 013 | Integration tests |
| FR-AGT-001 | Agent budgets, stop, repeat detection | 5 | 013 | Budget/cancellation tests |
| FR-KB-001 | Hybrid retrieval FTS5 + sqlite-vec | 3 | 011 | BM-07, retrieval tests |
| FR-KB-002 | Optional Qdrant CPU adapter | 7 | 011 | Adapter contract tests, BM-08 |
| NFR-OBS-001 | Local traces and metrics | 1, 6 | 014 | Unit tests |
| NFR-PERF-001 | Targets set from measurement | 6 | 014 | Result files, thresholds gate |
| NFR-HW-001 | CPU-only baseline fully supported | 1 | 009, 015 | AC-HW-003 |
| NFR-HW-002 | Optional validated GPU with CPU fallback | 1 | 015 | AC-HW-004 |
| NFR-HW-003 | One chat + one embedding model resident | 1 | 015 | AC-HW-005 |
| NFR-HW-004 | Every request sets num_ctx, num_gpu, keep_alive, num_predict | 1 | 010 | Provider tests |
| NFR-HW-005 | Adaptive profiles from probe and overrides | 1 | 015 | AC-HW-002 |
| NFR-HW-006 | Embeddings paused during generation (recommended) | 3 | 015 | Governor tests |
| NFR-HW-007 | Memory headroom exposed in capacity endpoint | 1 | 015 | API test |
| NFR-HW-008 | Disk floor warning | 1 | 015 | Health script test |
| NFR-HW-009 | Browser stays responsive during generation | 6 | 015 | BM-12, AC-HW-006 |
| NFR-HW-010 | Benchmarks harness-produced and schema-valid | 6 | 014 | AC-HW-008 |
| NFR-HW-011 | No threshold gate passes with null limits | 6 | 014 | Gate test |
| NFR-HW-012 | Model licenses reviewed before recommendation | 1 | 009 | Manifest review |
| FR-OPS-001 | One-command start | 1 | 015 | AC-HW-007 |
| FR-OPS-002 | One-command stop, only recorded processes | 1 | 015 | AC-HW-007 |
| FR-OPS-003 | Health check with exit codes 0/1/2 | 1 | 015 | Script tests |
| FR-OPS-004 | Hardware probe | 1 | 015 | AC-HW-001 |
| FR-OPS-005 | Diagnostics bundle without secrets | 1 | 015 | Redaction test |
