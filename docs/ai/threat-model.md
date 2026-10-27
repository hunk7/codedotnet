# Threat Model

Status: **Reviewed against the implementation; not externally audited.** Baseline: [`../context/security-and-privacy-baseline.md`](../context/security-and-privacy-baseline.md).

| Threat | Mitigation | Evidence |
|---|---|---|
| Other machines reach the service | Binds `127.0.0.1`; Qdrant URL must be loopback (config refuses otherwise) | Options tests; smoke run |
| A web page calls the service | Pairing token plus strict Origin/Host checks | Security middleware tests; smoke run (401 without token) |
| Pairing-code guessing | Short validity, lockout | Pairing tests |
| Browser sends the token elsewhere | Client refuses non-loopback URLs; token in memory only | `aiClient` tests |
| Prompt injection via knowledge-base text | Sources are data; no file/shell tools; code runs only in the sandbox | Design only; not adversarially tested |
| Model output rendered as HTML | Answers rendered as plain text | `AiPanel` test |
| Oversized input | 256 KiB body cap; question and document limits | API tests |
| Untrusted code execution | Only via `codedotnet-sandbox` with timeouts and payload caps | Sandbox tests |

Residual risks: an HTTPS-hosted page calling HTTP loopback depends on browser behavior and was **not** tested in real browsers. Prompt-injection resistance is not measured. A malicious local process running as the same user is out of scope.
