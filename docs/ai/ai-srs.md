# AI Software Requirements

Status: **Summary. The requirement table is [`requirements-traceability.md`](requirements-traceability.md).**

- The playground keeps working with no AI service (NFR-COMPAT-001).
- The service is local-first, loopback-only and has no mandatory recurring cost.
- Access requires pairing; browser origins are allow-listed.
- Knowledge-base answers carry citations or state that evidence is insufficient.
- Code execution happens only in the sandbox.
- Qdrant and GPU are optional; CPU-only with SQLite is the baseline.
- Performance and quality targets are not asserted until produced by the benchmark and evaluation harnesses.
