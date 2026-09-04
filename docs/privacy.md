# Privacy

## What codedotnet does with your code

codedotnet compiles and runs your C# code and STDIN entirely inside your own browser, using a
WebAssembly build of the .NET runtime and Roslyn running in a dedicated Web Worker. Your
program's source code, STDIN, and output are:

- **Never sent to any server.** codedotnet is a fully static site (hosted on GitHub Pages) with
  no backend API. There is nothing to send code to.
- **Persisted only locally**, in your browser's `localStorage`, scoped to this site's origin, so
  your work is restored the next time you open the page on the same browser/device. Clearing
  local data (via the in-app "Clear local data" control, or your browser's site data settings)
  removes it completely.
- **Not tracked by analytics.** codedotnet ships no third-party analytics or telemetry (NFR-S007).
  If analytics are ever introduced in a future version, they will never capture source code,
  input, output, or diagnostic content.

## Verifying this yourself (NFR-S008)

You do not need to take this on trust. You can verify there is no network transmission of your
code or input using your browser's developer tools:

1. Open codedotnet in your browser and open Developer Tools (F12 in most browsers).
2. Switch to the **Network** tab and enable "Preserve log".
3. Load the page fresh (reload) and let it finish initializing (the .NET runtime and worker
   assets will load — these are static files served from the same origin, e.g.
   `dotnet.wasm`, `*.dll`, `worker.js`).
4. Type or paste some C# source and STDIN, then click **Run**.
5. Inspect the Network tab: after the initial static asset load, no new network requests should
   appear as a result of clicking Run. All compile/execute activity happens via in-page
   `postMessage` calls to the Web Worker, which the Network tab does not show as HTTP traffic
   because no HTTP request is made.
6. Optionally, filter the Network tab to exclude the initial page load and confirm the request
   list stays empty while repeatedly running different programs.

This project's source contains no calls to `fetch`, `XMLHttpRequest`, `navigator.sendBeacon`, or
any HTTP client library outside of Vite/`dotnet.js`'s own static-asset loading — a repository
search for these APIs in `codedotnet.web/src` confirms this.

## A note on secrets

> Avoid entering passwords, API keys, private certificates, personal data, or production secrets
> into any coding playground, even when execution is local.

Although codedotnet does not transmit your code anywhere, it is still good practice to avoid
entering sensitive information into any code playground: your STDIN/source are stored in
browser `localStorage` in plain text, and copy-pasted code can end up in shell history, browser
autofill, or shared screenshots.
