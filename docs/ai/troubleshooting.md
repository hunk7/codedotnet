# Troubleshooting (Windows)

Status: **Draft; entries are expected failure modes, not yet reproduced.**

| Symptom | Likely cause | Check |
|---|---|---|
| Health reports Ollama failed | Service not running or not loopback | Ollama status, configured host |
| Model missing | Not pulled | `ollama list` |
| Out of memory or very slow | Profile too large | Switch to `low-resource`; see capacity endpoint |
| GPU mode falls back to CPU | Unsupported driver/runtime | Expected; CPU is the baseline |
| Browser cannot reach the AI service | Origin not allowed, CSP, or browser blocks loopback HTTP | Allowlist, CSP `connect-src`, browser console |
| Pairing code rejected | Code already used, expired (30 min) or from a previous run | Copy the newest "Pairing code" line from the service console or `.codedotnet/logs/ai.out.log` |
| Port in use (7900, 7901, 5173) | Another process | Configure port; do not kill unrelated processes |
| Sandbox cannot start | Worker assets not built | `npm run build:worker` |
| Embedding dimension mismatch | Model changed | Create a new versioned index |
| Script blocked | PowerShell execution policy | Use a process-scoped policy change; do not weaken machine policy |
| Stop script leaves a process | PID reused or process not recorded | Inspect `.codedotnet/run/pids.json` |
