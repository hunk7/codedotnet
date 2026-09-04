# ADR-006: GitHub Pages

## Status

Accepted

## Decision

Use GitHub Pages for the initial public deployment of codedotnet.

## Reasons

- Static hosting that matches the "browser-only, no server" architecture (see
  [ADR-001](001-browser-execution.md)).
- Free HTTPS out of the box, with custom-domain HTTPS enforcement available (see
  [`docs/custom-domains.md`](../custom-domains.md)).
- Git-based deployment: pushing to `main` triggers the build/deploy workflow directly (see
  [`docs/deployment.md`](../deployment.md)).
- No server to provision, patch, or maintain.
- Portfolio visibility: a public, shareable URL under the developer's own GitHub account/org.
