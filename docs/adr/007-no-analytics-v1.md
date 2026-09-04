# ADR-007: No analytics in Version 1

## Status

Accepted

## Decision

Do not include any third-party analytics or telemetry in version 1.

## Reasons

- Stronger, simpler privacy claim: codedotnet can truthfully state that no code, input, or usage
  data leaves the browser (see [`docs/privacy.md`](../privacy.md)).
- No accidental capture of source code, STDIN, or output by a third-party analytics SDK.
- Simpler Content-Security-Policy: no analytics domains need to be allow-listed in `connect-src`
  or `script-src` (see [`docs/security.md`](../security.md)).
- Smaller JavaScript payload and fewer runtime dependencies.

## Future consideration

If analytics are introduced in a later version, they must never capture source, input, output,
or diagnostic content (per NFR-S007 in the requirements document).
