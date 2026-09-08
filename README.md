# 🧩 codedotnet

> A browser-based C# playground — write, compile, and run C# entirely client-side using the
> **.NET 10 WebAssembly runtime** and **Roslyn**. No server. No account. No install.

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download)
[![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=white)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-strict-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Vite](https://img.shields.io/badge/Vite-Build-646CFF?logo=vite&logoColor=white)](https://vitejs.dev/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Deploy](https://img.shields.io/badge/Deploy-GitHub%20Pages-222?logo=githubpages&logoColor=white)](https://pages.github.com/)

**[🚀 Live demo →](https://<username>.github.io/codedotnet/)** _(replace with the actual deployed URL)_

---

## ✨ What it does

| | |
|---|---|
| 📝 | Monaco-powered editor for a single `Program.cs` file with C# syntax highlighting, inline diagnostics, and error navigation. |
| ▶️ | Click **Run** to compile and execute your code inside a dedicated Web Worker running Roslyn and the .NET WebAssembly runtime, entirely in your browser. |
| ⌨️ | Supply buffered STDIN before running, and see captured stdout/stderr, exceptions, and timing breakdowns (compile / execute / total) in the output pane. |
| 📊 | Automatic **time/space Big-O complexity estimate** (with rationale) for the code you just ran. |
| 🧱 | Resizable, swappable editor/I-O panes, focus modes, and fullscreen support. |
| 💾 | Editor preferences, theme (light/dark/system), and layout are persisted locally so your workspace is restored next time you visit. |
| ⚡ | Keyboard shortcuts: `Ctrl/Cmd+Enter` to run, `Shift+F5` to stop, `Ctrl/Cmd+S` to force-save locally, `F11` to toggle fullscreen, plus Monaco's native Find/Replace and Undo/Redo. |
| 🛑 | Graceful recovery from runaway programs (e.g. infinite loops): **Stop** hard-terminates the worker and a fresh one takes its place. |
| 🌐 | A clear "unsupported browser" message if required capabilities (WebAssembly, Web Workers, Blob URLs, local storage) are missing, instead of a silent failure. |

## 🚦 Quick start

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Node.js 22+](https://nodejs.org/).

```powershell
# Frontend (dev server with hot reload)
cd codedotnet.web
npm install
npm run dev

# Publish the compiler worker once so the frontend can load it locally
npm run build:worker
```

Then open the printed local URL in your browser.

To produce a production build (as used for GitHub Pages deployment):

```powershell
cd codedotnet.web
npm run build:worker:release
npm run build
```

## 🗂️ Repository layout

| Path | Purpose |
|---|---|
| 🖥️ `codedotnet.web/` | React + TypeScript + Vite frontend (editor, shell, worker manager, complexity estimator). |
| ⚙️ `codedotnet.compiler/` | .NET 10 `browser-wasm` project: Roslyn compile host, execution host, JS interop. |
| ✅ `codedotnet.compiler.tests/` | xUnit tests for the compiler/execution host logic (linked source, runs natively). |
| 🤖 `.github/workflows/deploy.yml` | CI validation + GitHub Pages deployment workflow. |
| 📦 `.github/dependabot.yml` | Automated weekly dependency updates (npm, NuGet, GitHub Actions). |
| 📚 `docs/` | Architecture, testing, deployment, security, privacy, limitations, and ADRs. |

## 📖 Documentation

- 🏗️ [`docs/architecture.md`](docs/architecture.md) — system architecture and component responsibilities.
- 🧪 [`docs/testing.md`](docs/testing.md) — how to run and what each test suite covers.
- 🚀 [`docs/deployment.md`](docs/deployment.md) — CI/CD and GitHub Pages deployment flow.
- 🌍 [`docs/custom-domains.md`](docs/custom-domains.md) — DNS/CNAME guidance for custom domains.
- 🔒 [`docs/security.md`](docs/security.md) — Content-Security-Policy, sandboxing model, dependency scanning.
- 🕵️ [`docs/privacy.md`](docs/privacy.md) — what happens to your code, and how to verify it yourself.
- ⚠️ [`docs/known-limitations.md`](docs/known-limitations.md) — buffered STDIN, single-file constraint, no NuGet restore, browser resource limits.
- 📝 [`docs/adr/`](docs/adr/) — Architecture Decision Records (ADR-001 through ADR-008).
- ©️ [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) — third-party license notices.

## 📜 License

MIT — see [`LICENSE`](LICENSE).

