# ADR-004: Single-file MVP

## Status

Accepted

## Decision

Support exactly one `Program.cs` file (top-level statements) per session, with no multi-file
project model.

## Reasons

- Lower complexity across the compiler host, persistence layer, and UI.
- Simpler compiler contract: a single named syntax tree in, a single assembly out.
- Faster delivery for version 1.
- Fewer persistence and UI states to design, test, and document (no file tree, no tabs, no
  file-level diagnostics routing).
- Adequate for the primary use case: algorithm practice, quick experiments, and interview-style
  programs.

## Related limitation

See [`docs/known-limitations.md`](../known-limitations.md) for the corresponding "single-file
constraint" and "no NuGet restore" limitations that follow from this decision.
