# CodeDotNet: Documentation Index

Status values: **Current** (matches source), **Stale** (contradicts source), **Legacy** (historical baseline), **New** (this PR). "Update in" names the phase that must correct it; this PR changes none of the existing files.

| Document | Purpose | Status | Issue | Update in |
|---|---|---|---|---|
| `README.md` | Project intro | Stale | References `codedotnet.compiler.tests` and engine paths under `codedotnet.compiler/` | Phase 0 |
| `docs/architecture.md` | Architecture | Stale | Same stale paths; does not cover sandbox/AI | Phase 0, then Phase 1 |
| `docs/testing.md` | Testing guide | Stale | `codedotnet.compiler.tests`; no sandbox tests | Phase 0 |
| `docs/deployment.md` | Deployment | Stale | `codedotnet.compiler.tests` | Phase 0 |
| `docs/security.md` | Security | Stale | Says `connect-src 'self'`; `index.html` also allows loopback `ws://` | Phase 0 |
| `docs/privacy.md` | Privacy | Current today, will become stale | Claims no WebSocket/fetch and "never sent to any server" | Phase 1 |
| `docs/known-limitations.md` | Limitations | Stale | Engine path references | Phase 0 |
| `docs/custom-domains.md` | Pages domains | Current (lower confidence, not fully read) | | n/a |
| `THIRD-PARTY-NOTICES.md` | Notices | Stale | Missing `ws`, `Basic.Reference.Assemblies`, possibly others | Phase 0 |
| `docs/adr/001..008` | Decisions | Current | Index `README.md` extended in this PR | n/a |
| `docs/adr/009..015` | AI decisions | New | Proposed status | This PR |
| `docs/codedotnet-project-requirements-and-architecture.md` | Legacy requirements (2,573 lines) | Legacy | Proposed repo structure differs from actual | n/a |
| `docs/codedotnet-implementation-instruction-set.md` | Legacy instructions | Legacy (not fully read) | | n/a |
| `docs/research/CODEDOTNET_SANDBOX_REQUIREMENTS_ARCHITECTURE.md` | Sandbox SRS (1,900 lines) | Legacy/reference | Only first 80 lines read | n/a |
| `docs/research/IMPLEMENTATION_STATUS.md` | Sandbox status | Current | Lists deferred items | n/a |
| `docs/research/SPIKE_FINDINGS.md` | Spike results | Current | Environment: Node 22.14.0, SDK 10.0.401, runtime 10.0.12 | n/a |
| `docs/research/dotnet-web-worker-notes.md` | Notes | Not read | | n/a |
| `docs/research/testing.md` | Duplicate-ish testing doc | Not read | | n/a |
| `docs/research/# CODEDOTNET_PROJECT_APPROACH.md` | Approach | Not read | Filename starts with `# ` | n/a |
| `docs/research/codedotnet-ai-zero-cost-feasibility-and-engineering-plan.md` | AI proposal | Input (untracked, unchanged) | Assumes stale paths, Qdrant-first, separate stdout/stderr; challenged in [`../ai/feasibility.md`](../ai/feasibility.md) | n/a |
| `docs/context/*` | Context package | New | | This PR |
| `docs/ai/*` | AI plan and stubs | New | | This PR |

## Source of truth order

1. Source code and project files.
2. `docs/context/*` (derived from source, dated by this PR).
3. ADRs.
4. Older guides and research (verify before relying on them).
