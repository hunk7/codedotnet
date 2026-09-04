# ADR-002: React and TypeScript

## Status

Accepted

## Decision

Use React and strict-mode TypeScript for the frontend application shell.

## Reasons

- Modular component architecture that maps cleanly onto the shell's discrete responsibilities
  (toolbar, editor adapter, I/O panel, layout manager, settings service, etc. — see
  [`docs/architecture.md`](../architecture.md)).
- Strong ecosystem and tooling (Vite, Vitest, React Testing Library, ESLint).
- Testability: components and hooks are straightforward to unit test in isolation.
- Straightforward integration with Monaco Editor via a ref-based adapter component.
- Portfolio/engineering-demonstration relevance: a widely recognized, production-representative
  stack.
