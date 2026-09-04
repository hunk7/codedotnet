# Security

## Execution sandboxing caveat (NFR-S003)

User-supplied C# code executes in a dedicated Web Worker, separate from the UI thread. This
provides process/thread isolation from the main page (a crash or hang in user code cannot block
the UI thread) and leverages the browser's WebAssembly sandbox, but **this is not described as a
perfect security boundary**: it relies on the browser vendor's own sandbox and WebAssembly
runtime implementation for isolation guarantees, the same way it would for any in-browser code
execution tool. codedotnet does not claim to defend against browser or WebAssembly engine
vulnerabilities themselves, and does not attempt additional sandboxing (e.g. `iframe` sandboxing
or a separate origin) beyond the Worker boundary and the Content-Security-Policy below.

## Content Security Policy

`codedotnet.web/index.html` sets the following Content-Security-Policy via a `<meta>` tag
(GitHub Pages serves static files only, so response headers cannot be customized; a `<meta>`
tag is the only mechanism available for a CSP on this hosting target):

```
default-src 'self';
script-src 'self' 'wasm-unsafe-eval';
style-src 'self' 'unsafe-inline';
worker-src 'self' blob:;
connect-src 'self';
img-src 'self' data:;
font-src 'self' data:;
object-src 'none';
base-uri 'self';
form-action 'self'
```

Rationale for each directive:

- **default-src 'self'** — Baseline: only same-origin resources are allowed unless a more
  specific directive below overrides it. codedotnet is a fully client-side, single-origin app
  with no third-party API calls, CDNs, or analytics.
- **script-src 'self' 'wasm-unsafe-eval'** — Scripts must come from the same origin. The
  `wasm-unsafe-eval` keyword is required because the .NET WebAssembly runtime (`dotnet.js` /
  `dotnet.wasm`) compiles and instantiates WebAssembly modules, which browsers gate behind this
  keyword instead of the broader (and unnecessary) `unsafe-eval`.
- **style-src 'self' 'unsafe-inline'** — Monaco Editor injects inline `<style>` tags at runtime
  for syntax highlighting themes and per-token CSS classes generated dynamically; `unsafe-inline`
  is required for the editor to render correctly. No inline `<script>` execution is permitted by
  this policy (`script-src` does not include `unsafe-inline`), so this does not reopen the
  primary XSS attack surface.
- **worker-src 'self' blob:** — The compiler pipeline runs inside a dedicated Web Worker loaded
  from the same origin. `blob:` is included because Vite's worker bundling can produce a
  worker script wrapped in a Blob URL in some build configurations.
- **connect-src 'self'** — The app never calls external network APIs; all compilation and
  execution happen locally in-browser via the worker and WebAssembly runtime, so only
  same-origin `fetch`/XHR (e.g. loading `dotnet.wasm` assets) is permitted.
- **img-src 'self' data:** — Favicon and any inline data-URI icons are same-origin or `data:`.
- **font-src 'self' data:** — No external font CDNs are used.
- **object-src 'none'** — No Flash/plugin content is used; fully disabled per CSP best practice.
- **base-uri 'self'** — Prevents a `<base>` tag injection attack from rebasing relative URLs to
  an attacker-controlled origin.
- **form-action 'self'** — The app has no forms that submit anywhere; restricts any accidental
  or injected form submission to the same origin.

## Dependency updates and scanning

[`.github/dependabot.yml`](../.github/dependabot.yml) configures weekly automated dependency
update pull requests for:

- **npm** packages in `codedotnet.web` (React, Vite, Monaco, Vitest, and related tooling).
- **NuGet** packages in `codedotnet.compiler` (Roslyn/`Microsoft.CodeAnalysis.CSharp` and the
  .NET SDK-provided packages referenced by the browser-wasm worker project).
- **GitHub Actions** used by [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml).

Dependabot pull requests still go through the existing CI validation job (build, test, and
artifact checks) before merge, so no dependency update is applied to `main` without passing the
same checks as a normal contribution. GitHub's Dependabot also performs automated vulnerability
scanning (via GitHub Advisory Database) against both the npm lockfile (`package-lock.json`) and
NuGet package references, opening security-update pull requests independently of the scheduled
version-update runs above.


## Threat model notes

- codedotnet executes arbitrary, user-supplied C# code. This code runs **only** inside a
  sandboxed WebAssembly Web Worker in the user's own browser — it never executes on any server,
  and it has no network or filesystem access beyond what the browser sandbox and this CSP allow.
  There is no server-side component to compromise.
- User source code and STDIN are persisted only to the browser's own `localStorage`, scoped to
  the site's origin; no data is transmitted anywhere.
