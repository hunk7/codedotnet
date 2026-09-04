# Deployment

codedotnet is deployed as a static site to GitHub Pages via
[`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml) ("CI / Deploy"). There is no
backend/server component to deploy.

## Triggers

- `pull_request` targeting `main` — runs the `validate` job only.
- `push` to `main` — runs `validate`, then `deploy`.
- `workflow_dispatch` — manual run from the Actions tab.

## `validate` job (runs on every PR and push to `main`)

Implements §18.1 PR validation:

1. Checkout, set up .NET 10 and Node 22.
2. `dotnet restore` + `dotnet build` for `codedotnet.compiler` (Release, browser-wasm).
3. Discover and run any `.csproj` referencing `Microsoft.NET.Test.Sdk` via
   `dotnet test -c Release` (currently `codedotnet.compiler.tests`; see
   [`docs/testing.md`](testing.md)).
4. `npm ci` in `codedotnet.web`.
5. `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm test`.
6. `npm run build` (production build with the default/root base path).
7. Verify required build assets exist: `dist/index.html` and at least one JS file under
   `dist/assets`.

## `deploy` job (runs only on push to `main`, after `validate` passes)

Implements §18.2 main-branch deployment:

1. Checkout, set up .NET 10 and Node 22, `npm ci`.
2. `npm run build:worker:release` — publishes the `codedotnet.compiler` browser-wasm worker in
   Release configuration and copies its output into `codedotnet.web/public/dotnet-worker`
   (see `scripts/copy-worker-assets.ps1`).
3. Re-run typecheck, lint, and frontend tests.
4. Compute the GitHub Pages base path as `/<repository-name>/` (matches
   `https://<username>.github.io/codedotnet/`, per §17.2).
5. `npm run build` with `VITE_BASE_PATH` set to that subpath and `VITE_COMMIT_SHA` set to the
   deploying commit, so the built `index.html` and asset URLs are correctly rooted under the
   Pages subpath and the About dialog can display the deployed commit.
6. Verify the build output: `index.html` references the expected base path, and
   `dist/dotnet-worker` contains a `worker.js`.
7. Report the total build artifact size (§18.4).
8. Upload the Pages artifact (`actions/upload-pages-artifact`) and deploy it
   (`actions/deploy-pages`), reporting the deployment URL as the job's `environment.url`.

## Custom domains and HTTPS

Per §17.3–17.4 of the requirements, a custom domain (e.g. `codedotnet.in`) and GitHub Pages'
HTTPS enforcement are configured directly in the repository's GitHub Pages settings once DNS is
verified; this is an operational/administrative step outside of the workflow file itself and is
not automated by CI.

## Local equivalents

You can reproduce the CI build steps locally:

```powershell
cd codedotnet.web
npm ci
npm run typecheck
npm run lint
npm run format:check
npm test
npm run build              # production build, root base path
npm run build:worker:release  # publish worker + copy assets (Release)
```

See [`docs/testing.md`](testing.md) for what each test suite covers, and
[`docs/architecture.md`](architecture.md) for how the pieces fit together.
