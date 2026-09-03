# codedotnet Implementation Instruction Set

## React + TypeScript + .NET 10 WebAssembly Worker

**Document type:** Step-by-step implementation playbook
**Project:** codedotnet
**Architecture:** React + TypeScript UI with a .NET 10 WebAssembly Web Worker
**IDE:** Visual Studio Professional
**Hosting:** GitHub Pages
**Source model:** One C# file named `Program.cs`
**Authentication:** None
**Backend:** None
**Target domains:** `codedotnet.in` and `codedotnet.com`
**Document version:** 1.0

> This document is an execution guide. Follow the phases in order. Do not begin the polished editor UI until the .NET WebAssembly worker, Roslyn compilation, program execution, output capture, and worker termination feasibility tests have passed.

---

# 1. Objective

Build **codedotnet**, a clean, modern, browser-only C# playground that:

- Uses React and TypeScript for the user interface.
- Uses Monaco Editor for editing one `Program.cs` file.
- Uses Roslyn to compile C#.
- Uses the .NET 10 WebAssembly runtime.
- Runs compilation and user code inside a dedicated Web Worker.
- Captures compiler diagnostics, console output, and runtime failures.
- Allows a running or infinite program to be stopped by terminating the worker.
- Requires no login, authentication, backend API, database, or server-side compiler.
- Builds to static assets that can be deployed to GitHub Pages.
- Supports a future custom domain such as `codedotnet.in` or `codedotnet.com`.

---

# 2. Non-Negotiable Architecture

Use this architecture throughout Version 1:

```plaintext
GitHub Pages
     |
     | Static HTML, CSS, JavaScript, WASM and assemblies
     v
Browser Main Thread
     |
     +-- React + TypeScript application
     +-- Monaco Editor
     +-- Toolbar and layout
     +-- STDIN, output and diagnostics
     +-- Settings and local persistence
     +-- Worker lifecycle manager
              |
              | Typed postMessage protocol
              v
Dedicated Web Worker
     |
     +-- .NET 10 WebAssembly runtime
     +-- Roslyn compiler
     +-- Curated framework references
     +-- In-memory assembly emission
     +-- Program execution host
     +-- Console and diagnostics capture
```

Do not add any of the following to the MVP:

- ASP.NET Core backend
- Web API
- MVC or Razor Pages
- Blazor Server
- Database
- Authentication
- Cloud source storage
- Multiple source files
- Arbitrary NuGet restoration
- Server-hosted code execution

---

# 3. Required Development Environment

## 3.1 Visual Studio workloads

Open:

```plaintext
Visual Studio Installer
→ Select Visual Studio Professional
→ Modify
```

Install or verify:

- ASP.NET and web development
- Node.js development
- .NET WebAssembly build tools
- Git for Windows
- .NET 10 SDK

## 3.2 External tools

Install:

- Current Node.js LTS
- Git
- A Chromium-based browser for primary development
- Microsoft Edge, Chrome, and Firefox for validation

## 3.3 Verify the environment

Open PowerShell or the Visual Studio integrated terminal and run:

```powershell
dotnet --version
dotnet --list-sdks
dotnet workload list
node --version
npm --version
git --version
```

Acceptance checks:

- `dotnet --version` begins with `10.`.
- A .NET 10 SDK appears in `dotnet --list-sdks`.
- Node and npm return valid versions.
- Git returns a valid version.

If the required WebAssembly workload is absent, run:

```powershell
dotnet workload install wasm-tools
dotnet workload install wasm-experimental
```

Restart Visual Studio after installing workloads.

---

# 4. Create the Repository and Visual Studio Solution

## 4.1 Create the root directory

Use a short path to avoid path-length and tooling problems.

## 4.2 Create a blank solution

In Visual Studio:

```plaintext
File → New → Project → Blank Solution
```

## 4.3 Initialize Git

```powershell
git init
git branch -M main
```

## 4.4 Create the initial directories

```powershell
mkdir docs
mkdir tests
mkdir scripts
```

## 4.5 Add the approved requirements file

Copy the previously generated requirements document into:

```plaintext
docs/codedotnet-project-requirements-and-architecture.md
```

Place this implementation guide at:

```plaintext
docs/codedotnet-implementation-instruction-set.md
```

## 4.6 Create `.gitignore`

Create a root `.gitignore` covering Visual Studio, .NET, Node, Playwright, logs, OS, and local environment artifacts.

## 4.7 Create `.editorconfig`

Use a root `.editorconfig` to keep C#, TypeScript, JSON, YAML, Markdown, and CSS formatting consistent: UTF-8, final newline, spaces instead of tabs, four spaces for C#, two spaces for TypeScript/JSON/YAML/CSS, trim trailing whitespace except in Markdown.

## 4.8 First commit

```powershell
git add .
git commit -m "chore: initialize codedotnet solution and documentation"
```

---

# 5. Create the React + TypeScript Project

## 5.1 Preferred Visual Studio route

Right-click Solution → Add → New Project → React App (TypeScript).

## 5.2 CLI fallback

```powershell
npm create vite@latest codedotnet.web -- --template react-ts
cd codedotnet.web
npm install
```

## 5.3 Verify the development server

```powershell
npm run dev
```

## 5.4 Verify a production build

```powershell
npm run build
```

## 5.5 Initial frontend cleanup

Remove the sample counter, Vite logo, React logo, and demonstration CSS. Create a minimal placeholder screen:

```plaintext
codedotnet
Loading development environment...
```

## 5.6 Add strict quality tools

Configure TypeScript strict mode, ESLint, Prettier, Vitest, and React Testing Library.

## 5.7 Commit the React foundation

```powershell
git add .
git commit -m "feat: add React TypeScript frontend"
```

---

# 6. Study the Official Web Worker Pattern Before Coding

Before implementing codedotnet's worker integration, study Microsoft's .NET 10 React Web Worker sample named `DotNetOnWebWorkersReact`.

Focus on:

- How the React application starts the worker.
- How the worker loads the .NET runtime.
- How JavaScript calls exported .NET methods.
- How .NET returns data to the main thread.
- How published .NET assets are copied into the React static directory.
- How development and release builds differ.
- How asset paths are constructed.

Create notes in `docs/research/dotnet-web-worker-notes.md`.

Exit criterion: the developer can explain the full path from a button click in React to a C# method running inside the Web Worker and returning a result.

---

# 7. Create the .NET 10 WebAssembly Worker Project

## 7.1 Project name

Create `codedotnet.compiler`. This project owns runtime integration, Roslyn compilation, reference loading, in-memory assembly creation, program execution, console redirection, and diagnostic transport models.

## 7.3 Initial project goal

React button → post message to worker → start .NET 10 in worker → call exported C# runtime-information method → return runtime information → show information in React. Do not add Monaco or Roslyn in this step.

## 7.4 Initial exported C# behavior

Create a small exported C# method that returns structured runtime information, including `Environment.Version`, `RuntimeInformation.FrameworkDescription`, and architecture where available.

## 7.5 Worker asset integration

Add a repeatable build script that publishes `codedotnet.compiler`, clears the previous integrated worker output, copies only required published assets into `codedotnet.web/public/dotnet-worker/`, and preserves predictable paths in development and GitHub Pages production builds.

## 7.6 Initial worker communication

Define a small message contract: `InitializeRuntime` request, `InitializationProgress` event, `RuntimeReady` event, `RuntimeError` event, `GetRuntimeInformation` request, `RuntimeInformation` response. Every request and response must include protocol version, request ID, operation/event type, and payload.

## 7.7 First feasibility acceptance criteria

- React UI remains responsive while the runtime loads.
- Runtime loading occurs inside the worker.
- The displayed framework description confirms .NET 10.
- A runtime failure produces a visible error and Retry action.
- Repeated runtime-information calls do not reload the runtime.

## 7.8 Commit

```powershell
git add .
git commit -m "feat: run dotnet 10 in a browser web worker"
```

---

# 8. Implement Worker Lifecycle and Recovery

Complete this stage before adding Roslyn.

## 8.1 Worker states

`Uninitialized, Initializing, Ready, Busy, Stopping, Stopped, Failed, Recovering`. Use one state machine rather than unrelated Boolean values.

## 8.2 Worker manager responsibilities

The React-side worker manager shall create the worker, initialize the runtime, track the active request ID, route messages, reject stale messages, apply operation timeouts, terminate the worker, create a replacement worker, and restore Ready state after recovery.

## 8.3 Termination test operation

Create a temporary worker operation that intentionally never completes or runs long enough to test Stop.

Validation sequence: start long operation, confirm the React page remains responsive, press Stop, terminate the worker, create a replacement worker, wait for runtime initialization, call the runtime-information method again, confirm success.

## 8.4 Timeout

Add a development timeout to the temporary operation. The final MVP execution timeout will default to five seconds, but runtime initialization must use a separate and longer timeout.

## 8.5 Commit

```powershell
git add .
git commit -m "feat: add worker lifecycle termination and recovery"
```

---

# 9. Add Roslyn Compilation

Only start this phase after the Web Worker feasibility and recovery checks pass. Add `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` package references. Structure the compiler modules under `Compilation/`, `Contracts/`, `Execution/`, `Interop/`, and `Runtime/`. Start with hard-coded source, then connect the editor later.

---

# 10. Execute the Emitted Assembly

Extend the worker pipeline: C# source → Roslyn compilation → in-memory assembly → entry-point execution → captured console output. Redirect console streams with `try/finally`, treat STDIN as a fully buffered input, apply a 100,000-character output limit, and enforce a 5-second execution timeout by terminating the worker from the main thread.

---

# 11-27 (Later Phases)

Sections covering the Monaco Editor UI, layout and fullscreen implementation, toolbar/status bar, theming, local persistence, testing instructions, performance instructions, GitHub Actions CI, GitHub Pages deployment, custom domain instructions, git/work management, safe AI assistance usage, the first two-week execution schedule, phase gates, and the definition of done are deferred until milestones M3 and beyond, per the phased delivery plan. Refer to the full architecture specification (`docs/codedotnet-project-requirements-and-architecture.md`) for the corresponding functional and non-functional requirements that will govern those phases.

---

# 28. Official Technical References

- Microsoft Learn, .NET on Web Workers: `https://learn.microsoft.com/aspnet/core/client-side/dotnet-on-webworkers`
- Microsoft .NET 10 React Web Worker sample: `https://github.com/dotnet/blazor-samples/tree/main/10.0/DotNetOnWebWorkersReact`
- Microsoft Learn, host and deploy Blazor WebAssembly/static WebAssembly applications: `https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/`
- Monaco Editor: `https://microsoft.github.io/monaco-editor/`
- Monaco Editor API: `https://microsoft.github.io/monaco-editor/typedoc/`
- Vite documentation: `https://vite.dev/guide/`
- GitHub Pages documentation: `https://docs.github.com/pages`
- GitHub Pages limits: `https://docs.github.com/pages/getting-started-with-github-pages/github-pages-limits`
