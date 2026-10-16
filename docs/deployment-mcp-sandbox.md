# Deploying codedotnet-sandbox (MCP WebSocket Server) to Render

This is the operational runbook for hosting the existing Node MCP sandbox (`codedotnet-sandbox/`)
as a public, always-on-URL (but free-tier, sleep-when-idle) WebSocket backend, so the
GitHub Pages–hosted MCP Playground (`codedotnet.web`) can connect to it from the public internet.

No sandbox isolation logic changed: `workerPool.mjs`, `worker.mjs`, and `mcpCore.mjs` are deployed
exactly as committed. Only a Docker packaging layer (`codedotnet-sandbox/Dockerfile`,
`.dockerignore`) and a Render Blueprint (`render.yaml`) were added, plus the frontend was pointed
at the new public endpoint (`codedotnet.web/index.html` CSP, `McpPlayground.tsx` default URL).

## 1. Prerequisites

- A Render account (free tier) connected to the `hunk7/codedotnet` GitHub repo.
- Push access to the repo (Render Blueprints deploy from a branch — `render.yaml` targets `main`).

## 2. First-time deployment

1. In the Render dashboard: **New → Blueprint**.
2. Select the `hunk7/codedotnet` GitHub repo. Render will detect `render.yaml` at the repo root
   and propose the `codedotnet-sandbox` web service (Docker runtime, free plan, Oregon region).
3. Click **Apply**. Render will:
   - Clone the repo.
   - Build `codedotnet-sandbox/Dockerfile` using the **repo root** as build context (this is
     required — the image needs `CodeDotNet.Contracts/`, `CodeDotNet.Engine/`, and
     `codedotnet.compiler/` in addition to `codedotnet-sandbox/` itself).
   - Stage 1 (`mcr.microsoft.com/dotnet/sdk:10.0`) installs the `wasm-tools` workload and runs
     `dotnet publish codedotnet.compiler` to produce the browser-wasm engine artifact.
   - Stage 2 (`node:22-slim`) copies only the sandbox source + the published wasm artifact and
     starts `node src/wsServer.mjs`.
4. The first build takes several minutes (wasm-tools workload install + wasm publish). Watch the
   Render build logs for errors.
5. Once live, Render assigns a public hostname, typically `https://codedotnet-sandbox.onrender.com`
   (the exact subdomain depends on availability/service name). Confirm it in the Render dashboard.

## 3. Post-deploy configuration (required before treating this as "live")

1. **Set `MCP_ALLOWED_ORIGIN`** in the Render dashboard's Environment settings for the service
   (NOT in `render.yaml`, so it can be rotated without a redeploy/code change):
   - Start with `*` for initial smoke testing (step 4 below).
   - Once confirmed working, tighten it to the exact GitHub Pages origin, e.g.
     `https://hunk7.github.io` (origin only — no path). This is enforced by
     `wsServer.mjs`'s `isOriginAllowed()`.
2. **Confirm the hostname matches the frontend.** If Render assigned something other than
   `codedotnet-sandbox.onrender.com`, update:
   - `codedotnet.web/index.html` — CSP `connect-src` entry (`wss://<actual-host>`).
   - `codedotnet.web/src/mcp/McpPlayground.tsx` — `DEFAULT_MCP_URL` constant.
   - This runbook (section 2, step 5).

## 4. Smoke testing

After the service is live and `MCP_ALLOWED_ORIGIN` is set to `*` (temporarily):

1. Open the deployed GitHub Pages site, navigate to the MCP Playground tab.
2. Click **Connect** using the default public URL. Expect `Connecting…` (cold-start hint shown),
   then `Connected` within up to ~60 seconds if the free instance was asleep.
3. Confirm the tool list populates (`tools/list` round-trip).
4. Run a simple `compile_and_run` call (e.g. `Console.WriteLine("hi");`) and confirm output.
5. Re-run immediately (no cold start this time) and confirm latency is low (<2s round trip).
6. Test an intentionally slow/infinite-loop snippet and confirm the `TimedOut` status returns
   instead of hanging the browser.
7. Once confirmed, switch `MCP_ALLOWED_ORIGIN` to the exact Pages origin and repeat step 2–4 to
   confirm the tightened CORS/origin check still allows the real site (and, optionally, verify a
   request from a different origin is rejected).

## 5. Known free-tier limitations

- **Cold start / sleep-after-idle**: Render free web services sleep after ~15 minutes with no
  inbound traffic. The first WebSocket connection after a sleep period can take up to ~60 seconds
  while the container restarts. The frontend surfaces this via the cold-start hints in
  `McpPlayground.tsx`.
- **Single instance / concurrency ceiling**: The worker pool (`workerPool.mjs`) defaults to 2
  concurrent compile workers per instance. Render's free plan runs a single instance (no
  horizontal scaling), so more than 2 simultaneous `compile_and_run` calls queue behind each other
  (polled every 25ms) rather than failing outright, but add latency under load.
- **Memory/CPU limits**: Render free web services have constrained CPU/RAM; very large compiles or
  many concurrent users may hit limits. Monitor Render's metrics dashboard if this becomes an issue.

## 6. Rollback

- **Frontend-only rollback**: revert the CSP (`codedotnet.web/index.html`) and `DEFAULT_MCP_URL`
  (`McpPlayground.tsx`) commits; the Playground will fall back to requiring a local
  `ws://127.0.0.1:7900` sandbox (original behavior), no Render dependency.
- **Full rollback**: In the Render dashboard, suspend or delete the `codedotnet-sandbox` service.
  Deleting `render.yaml` does not affect an already-provisioned service — it must be removed from
  the Render dashboard directly.
- Render also keeps prior deploys; use **Rollback to a previous deploy** in the service's Deploys
  tab if a bad commit breaks the running service, instead of a git revert + redeploy cycle.
