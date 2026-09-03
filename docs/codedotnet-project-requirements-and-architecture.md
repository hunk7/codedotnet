# codedotnet

## Product Requirements, Software Requirements, and Architecture Specification

**Project name:** codedotnet
**Preferred domains:** `codedotnet.in` and `codedotnet.com`
**Document status:** Approved planning baseline
**Document version:** 1.0
**Target release:** Version 1 MVP
**Primary platform:** Desktop web browsers
**Hosting target:** GitHub Pages
**Runtime target:** .NET 10 WebAssembly
**Language target:** C# using the Roslyn compiler bundled with the application

> **Domain assumption:** The originally supplied suffix `.con` has been interpreted as `.com`, because `.con` is not a standard public top-level domain. The intended public names are therefore `codedotnet.in` and `codedotnet.com`. Domain registration and ownership are separate from GitHub Pages hosting and may involve a recurring registrar charge.

---

# 1. Executive Summary

codedotnet is a privacy-first, browser-based C# development environment intended for learning, interview preparation, algorithm experimentation, and quick testing of modern C# code. The application will provide a clean, modern, minimal IDE-style interface inspired by the general usability patterns of browser coding products, while maintaining completely original branding, colors, components, and interaction design.

The defining architectural feature of codedotnet is that C# compilation and program execution occur entirely within the user's browser. The application will use the .NET 10 WebAssembly runtime and Roslyn compiler libraries loaded as static web assets. A dedicated Web Worker will isolate compilation and execution from the main UI thread, preserve editor responsiveness, and provide a recoverable mechanism for stopping long-running or infinite programs.

The Version 1 product is intentionally constrained to a single source file named `Program.cs`. It will not require authentication, include a backend server, store source in a cloud database, support arbitrary NuGet packages, or provide multiple-file projects. This controlled scope is essential for delivering a reliable, technically credible, and portfolio-quality release.

The compiled production application will consist only of static HTML, CSS, JavaScript, WebAssembly, .NET assemblies, Monaco Editor resources, and other versioned assets. This enables deployment through GitHub Pages under a URL such as:

- `https://<github-user>.github.io/codedotnet/`
- `https://codedotnet.in/`
- `https://codedotnet.com/`

The custom domains can point to the same GitHub Pages deployment after domain ownership and DNS configuration are completed.

---

# 2. Product Vision

> Build a fast, modern, private, and serverless C# coding environment that allows users to write, compile, and execute modern C# code using .NET 10 directly inside a desktop browser without registration, authentication, backend infrastructure, or server-side source-code processing.

---

# 3. Problem Statement

Many free online C# compilers present one or more limitations:

- They execute code using older Mono or .NET Framework-compatible runtimes.
- They do not accurately identify the compiler or runtime executing the program.
- They require login or registration for common functionality.
- They transmit source code to a remote compilation service.
- They depend on backend compute that introduces cost, security, and scaling concerns.
- They include advertisements or distracting controls.
- They are not optimized for C# learning, algorithm practice, or rapid code experimentation.
- Their interfaces provide limited control over editor appearance and layout.
- Public untrusted code execution creates significant backend security risk.

codedotnet addresses these issues by shifting the compilation and execution workload into the browser. This design removes the need for a compiler backend, avoids per-execution hosting costs, and makes source-code privacy a direct architectural property rather than only a policy statement.

---

# 4. Business and Product Objectives

## 4.1 Primary objectives

### OBJ-001: Modern C# execution

codedotnet shall compile C# using a Roslyn compiler packaged with a .NET 10 browser runtime. The application shall display the runtime and compiler information detected from the actual deployed build rather than relying only on hard-coded labels.

### OBJ-002: Browser-only architecture

All source compilation and user-program execution shall occur inside the browser. No codedotnet-controlled backend compilation API shall exist in Version 1.

### OBJ-003: Static deployment

The production application shall be deployable as static assets through GitHub Pages.

### OBJ-004: Immediate access

A visitor shall be able to open codedotnet and begin writing code without creating an account, signing in, accepting an identity flow, or providing personal information.

### OBJ-005: Professional developer experience

The product shall provide a polished single-file coding interface with a professional editor, diagnostics, console output, standard input, configurable layout, keyboard shortcuts, and theme controls.

### OBJ-006: Responsive execution

Compilation and execution shall not freeze the main application interface. Heavy work shall run in a dedicated Web Worker.

### OBJ-007: Privacy by architecture

Source code, standard input, output, diagnostics, and locally saved settings shall remain in the user's browser unless the user independently copies, downloads, or shares them.

### OBJ-008: Robust failure recovery

A hanging or crashing user program shall not require the visitor to close the browser tab or refresh the entire application. The execution worker shall be replaceable.

### OBJ-009: Portfolio-grade implementation

The project shall demonstrate SDE II-level engineering through requirements analysis, architecture decisions, modular design, performance measurement, failure handling, automated testing, CI/CD, security awareness, and technical documentation.

### OBJ-010: Original product identity

The project shall use the name **codedotnet** and original visual branding. The product shall not use OneCompiler, Microsoft, Visual Studio, .NET Fiddle, or other third-party branding in a manner that implies ownership, endorsement, or affiliation.

---

# 5. Success Metrics

The following measurements shall be collected from real builds and test devices. Values must not be invented for portfolio materials.

## 5.1 Functional success metrics

- A simple C# program compiles and executes successfully.
- Syntax errors generate accurate Roslyn diagnostics.
- Diagnostics map to the correct Monaco Editor line and column.
- `Console.Write` and `Console.WriteLine` output is captured.
- Buffered STDIN is available to programs where the runtime integration supports normal console input.
- Infinite loops can be stopped by terminating the execution worker.
- The user can run multiple programs sequentially without refreshing the application.
- Editor source and preferences survive a normal browser refresh.
- The static production build works from a GitHub Pages repository subpath.

## 5.2 Performance targets

Initial targets for a representative modern desktop and broadband connection:

- UI shell interactive: 2 seconds or less where network and device conditions allow.
- Cached application load: 2 seconds or less.
- Cached runtime readiness: 3 seconds or less.
- Warm compilation of a simple program: 1.5 seconds or less.
- Theme switch response: 100 milliseconds or less.
- Pane swap response: 100 milliseconds or less.
- Pane resizing: visually smooth without persistent jank.
- Editor typing during worker activity: no visible interruption.

These are engineering targets, not contractual guarantees. The performance report shall record the browser, device, network state, cache state, source size, build version, median, and percentile measurements.

## 5.3 Quality targets

- No critical accessibility failures in primary workflows.
- No known high-severity dependency vulnerabilities at release time.
- No source code transmitted by application-controlled network requests.
- All required CI checks pass before deployment.
- Critical execution-state and worker-recovery paths have automated test coverage.
- Production assets remain comfortably below GitHub Pages limits.

---

# 6. Stakeholders and Users

## 6.1 Stakeholders

- Product owner and primary developer
- C# learners
- Interview candidates
- .NET developers
- Open-source contributors
- Technical reviewers and hiring managers
- GitHub Pages as the static hosting provider
- Browser vendors and the WebAssembly platform

## 6.2 User personas

### Persona A: C# learner

Wants to practise syntax, collections, LINQ, object-oriented programming, recursion, trees, graphs, stacks, queues, heaps, and algorithms without installing tooling.

### Persona B: Interview candidate

Wants a simple, distraction-free environment for writing and testing C# interview solutions.

### Persona C: Experienced .NET developer

Wants to verify a small modern C# code sample quickly and inspect compilation errors or output.

### Persona D: Technical evaluator

Wants to assess the project's architecture, runtime integration, reliability, test strategy, and engineering trade-offs.

---

# 7. Product Scope

## 7.1 Version 1 in scope

- Original codedotnet branding
- Desktop-first responsive web application
- Single `Program.cs` source file
- Monaco Editor
- C# syntax highlighting
- .NET 10 WebAssembly runtime
- Roslyn compilation
- Controlled .NET reference assemblies
- Browser-local execution
- Dedicated Web Worker
- Console output capture
- Buffered standard input
- Compilation diagnostics
- Monaco error and warning markers
- Clickable diagnostic navigation
- Runtime exception display
- Run, Stop, Clear, Reset, and Download actions
- Editor and I/O panes
- Draggable pane divider
- Reversible left/right pane order
- Editor focus/full-screen mode
- I/O focus/full-screen mode
- Browser Fullscreen API integration where available
- Light, dark, and system themes
- Word wrap control
- Font size control
- Editor configuration
- Local autosave
- Local preference persistence
- Execution timeout
- Output-size limit
- Runtime loading progress
- GitHub Pages deployment
- GitHub Actions CI/CD
- Automated unit, integration, compiler-compatibility, and end-to-end tests
- Architecture, privacy, limitations, performance, and deployment documentation

## 7.2 Explicitly out of scope for Version 1

- Multiple source files
- File explorer
- Projects and solutions
- User registration
- Authentication or authorization
- User accounts and profiles
- Cloud source-code storage
- Database
- Server-side execution
- Arbitrary NuGet packages
- Git operations inside the application
- Collaborative editing
- Public code sharing
- Saved cloud snippets
- AI coding assistance
- Debugger and breakpoints
- Step-through execution
- Full Language Server Protocol implementation
- ASP.NET Core application hosting
- Native executable generation
- Windows-specific APIs
- Unrestricted network or filesystem access
- Mobile-first IDE support
- Commercial SaaS functionality on GitHub Pages

---

# 8. Assumptions, Constraints, and Dependencies

## 8.1 Assumptions

- Users access the product through a modern desktop browser.
- JavaScript, WebAssembly, and Web Workers are enabled.
- First-time users accept a larger download for runtime and compiler assets.
- Subsequent visits benefit from browser caching.
- User code is intended for short-running educational and experimental programs.
- STDIN can be supplied as a complete buffered string before execution.
- No backend service is available to compensate for unsupported WebAssembly APIs.

## 8.2 Constraints

- GitHub Pages serves static content and cannot run an ASP.NET Core backend.
- WebAssembly execution differs from unrestricted desktop CoreCLR execution.
- Browser memory and CPU restrictions vary by browser and device.
- Worker termination is the primary hard-cancellation mechanism for infinite programs.
- Arbitrary NuGet package restoration is outside the MVP.
- Monaco Editor is desktop-focused and is not officially designed as a mobile editor.
- GitHub Pages has published artifact and bandwidth limits.
- Domain registration for `codedotnet.in` and `codedotnet.com` is not free static hosting and may require recurring payment.
- A custom `.in` or `.com` domain is optional. The GitHub Pages URL can remain the cost-free public URL.

## 8.3 Major technical dependencies

- React
- TypeScript
- Vite
- Monaco Editor
- .NET 10 WebAssembly runtime
- Roslyn compiler libraries
- .NET reference assemblies
- Web Worker APIs
- Browser storage APIs
- GitHub Actions
- GitHub Pages
- Playwright
- Vitest and React Testing Library

---

# 9. User Experience Requirements

## 9.1 Target visual design

codedotnet shall use a clean, modern, and minimal IDE-style layout. The design may use familiar development-tool patterns but must use original visual assets and presentation.

Visual principles:

- Compact but readable toolbar
- Neutral page surfaces
- One primary accent color
- A distinct Run action color
- Clear visual hierarchy
- Consistent 8-pixel spacing system
- Subtle borders
- Limited decorative shadows
- Minimal animation
- Strong editor and console readability
- Clear active-pane indication
- No advertisements
- No login controls
- No unnecessary navigation

## 9.2 Default desktop layout

```plaintext
┌───────────────────────────────────────────────────────────────┐
│ codedotnet   .NET 10   Run   Stop   Clear   Layout   Settings │
├──────────────────────────────────┬────────────────────────────┤
│ Program.cs                       │ I/O                        │
├──────────────────────────────────┤ STDIN                      │
│                                  │                            │
│ Monaco C# Editor                 ├────────────────────────────┤
│                                  │ Output / Problems          │
│                                  │                            │
├──────────────────────────────────┴────────────────────────────┤
│ Ready | Ln 1, Col 1 | Spaces: 4 | C# | .NET 10 | Browser    │
└───────────────────────────────────────────────────────────────┘
```

Suggested initial pane proportions:

- Editor pane: 65%
- I/O pane: 35%
- STDIN section: 30% of I/O pane height
- Output section: 70% of I/O pane height

## 9.3 Branding and domain display

The visible product name shall be styled as `codedotnet`.

The About screen may show:

```plaintext
codedotnet
Browser-based C# playground
codedotnet.in | codedotnet.com
```

The interface should normally display only one canonical domain to avoid visual clutter. Both domains may redirect to the same deployment. A recommended approach is:

- Primary India-focused domain: `codedotnet.in`
- Global redirect or alternate domain: `codedotnet.com`

The repository and GitHub Pages deployment should continue working even when neither custom domain is registered.

---

# 10. Functional Requirements

## 10.1 Application Initialization

### FR-001: Static application launch

The application shall load through HTTPS from GitHub Pages or a custom domain mapped to GitHub Pages.

### FR-002: Progressive shell rendering

The page shell, toolbar, editor placeholder, and loading status shall become visible before the full compiler runtime is ready.

### FR-003: Runtime initialization status

The application shall expose the following statuses where applicable:

```plaintext
Loading codedotnet
Loading editor
Loading .NET runtime
Loading C# compiler
Preparing execution environment
Ready
```

### FR-004: Runtime initialization retry

When initialization fails, the user shall receive:

- A clear summary
- A Retry action
- A Reload action
- Browser compatibility guidance
- Expandable technical details

### FR-005: Runtime information

An About or Runtime Information dialog shall display detected values such as:

- Product version
- Build commit
- Build date
- .NET runtime version
- Framework description
- Roslyn compiler version
- C# language configuration
- Execution environment
- Hosting origin

### FR-006: No misleading version claim

The interface shall not claim `.NET 10` merely because a label was configured. The deployed build shall run an automated runtime-version smoke test, and the About dialog shall obtain the runtime description from the actual runtime.

---

## 10.2 Source Editor

### FR-010: Single source file

Version 1 shall expose exactly one source model named `Program.cs`.

### FR-011: No file creation

The UI shall not include a functional Add File action, multi-file explorer, rename action, or project tree.

### FR-012: Text editing

The editor shall support:

- Insert and delete
- Undo and redo
- Copy, cut, and paste
- Selection
- Multi-cursor editing
- Mouse and keyboard navigation
- Drag selection
- Line movement
- Indentation
- Comment toggling

### FR-013: C# syntax colorization

The editor shall use the Monaco C# language definition for syntax colorization.

### FR-014: Line numbers

Line numbers shall be enabled by default and configurable through editor settings.

### FR-015: Current-line highlighting

The cursor line shall be visually identifiable.

### FR-016: Bracket and quote assistance

The editor shall support:

- Matching brackets
- Auto-closing brackets
- Auto-closing quotes
- Auto-surround behavior
- Bracket-pair colorization
- Indentation guides

### FR-017: Find and replace

The editor shall support:

- Find
- Replace
- Replace all
- Match case
- Whole word
- Regular expressions

### FR-018: Word wrap

The user shall be able to choose:

- Off
- On
- Wrap at a configurable column

### FR-019: Font size

The user shall be able to select an editor font size from 10 to 28 pixels. The default shall be 14 pixels.

### FR-020: Font family

The editor shall use a suitable monospaced font stack by default. The user may choose from a small curated set or enter a local font-family value.

### FR-021: Line height

The user shall be able to adjust line height within a validated range.

### FR-022: Tab size

The user shall be able to select 2, 4, or 8 spaces. The default shall be 4 spaces.

### FR-023: Additional editor preferences

The settings panel should expose:

- Minimap visibility
- Line number visibility
- Whitespace rendering
- Sticky scroll
- Smooth scrolling
- Cursor style
- Cursor blinking
- Folding
- Auto indentation
- Format on paste where available
- Copy with syntax highlighting
- Bracket-pair colorization

### FR-024: Zoom actions

The editor shall provide font increase, font decrease, and reset actions.

Suggested shortcuts:

```plaintext
Ctrl/Cmd + Plus    Increase editor font size
Ctrl/Cmd + Minus   Decrease editor font size
Ctrl/Cmd + 0       Reset editor font size
```

### FR-025: Default source

The initial starter program shall be small and verify the runtime:

```csharp
using System;
using System.Runtime.InteropServices;

Console.WriteLine("Hello from codedotnet!");
Console.WriteLine(RuntimeInformation.FrameworkDescription);
```

### FR-026: Reset source

Reset shall restore the default program. If the current source differs from the default, the application shall request confirmation before replacing it.

### FR-027: Local autosave

The application shall save source locally after a debounce period.

Requirements:

- No network request
- No account
- Restore after refresh
- Graceful failure when storage is unavailable
- Versioned storage format
- Clear Local Data action

### FR-028: Download source

The user shall be able to download the current source as `Program.cs` using a browser-generated file.

### FR-029: Source-size limit

The application shall enforce a configurable maximum source size. The recommended initial limit is 500 KB.

### FR-030: Unsaved-state behavior

Because source is autosaved locally, the UI shall distinguish between:

- Saved locally
- Saving
- Local storage unavailable
- Storage quota exceeded

---

## 10.3 Compilation

### FR-040: Run action

The Run action shall create an immutable snapshot of the current source and STDIN, then send that snapshot to the worker.

### FR-041: Roslyn compilation

The worker shall compile `Program.cs` using the included Roslyn compiler.

### FR-042: Compilation configuration

The compilation host shall explicitly configure:

- Console application output
- C# parse options
- Language version behavior
- Nullable context behavior
- Optimization level
- Warning level
- Deterministic or reproducible options where applicable
- Required metadata references

### FR-043: Controlled framework references

A documented set of references shall support common types and namespaces including:

- `System`
- `System.Collections`
- `System.Collections.Generic`
- `System.Linq`
- `System.Numerics`
- `System.Text`
- `System.Text.Json`
- `System.Threading`
- `System.Threading.Tasks`

The supported surface shall be verified with automated compiler compatibility cases.

### FR-044: Diagnostic structure

Every compiler diagnostic returned to the UI shall contain:

- Diagnostic identifier
- Severity
- Message
- Filename
- Start line
- Start column
- End line
- End column

### FR-045: Diagnostic display

The Problems view shall render errors and warnings in a structured list.

Example:

```plaintext
Program.cs(12,18): error CS1002: ; expected
```

### FR-046: Monaco markers

Compiler diagnostics shall produce Monaco markers:

- Errors: red
- Warnings: amber or yellow
- Informational items: neutral or blue if displayed

### FR-047: Diagnostic hover

Hovering over a marked source location shall display the diagnostic message and code.

### FR-048: Diagnostic navigation

Selecting a diagnostic shall focus the editor, reveal the affected line, and place the cursor near the affected token.

### FR-049: Build status

The application shall show one of the following outcomes:

```plaintext
Build succeeded
Build succeeded with warnings
Build failed
Execution completed
Execution failed
Execution stopped
Execution timed out
```

### FR-050: Repeated compilation

The compiler and framework references shall remain initialized within a healthy worker so repeated runs do not repeat unnecessary setup.

### FR-051: Empty or invalid source

Empty source, missing entry-point behavior, scripting/top-level statement behavior, and invalid program shapes shall produce accurate diagnostics rather than internal application errors.

---

## 10.4 Execution

### FR-060: Browser-local execution

A successfully compiled assembly shall execute inside the browser worker.

### FR-061: Main-thread isolation

Compilation and execution shall not run directly in the main React UI thread.

### FR-062: Console output

The execution host shall capture:

- `Console.Write`
- `Console.WriteLine`
- Standard error where practical
- Unhandled exceptions

### FR-063: Buffered STDIN

The I/O pane shall provide a multiline STDIN field. Version 1 shall treat it as a complete input buffer supplied before execution.

Example input:

```plaintext
5
10 20 30 40 50
```

Example program:

```csharp
int count = int.Parse(Console.ReadLine()!);
int[] values = Console.ReadLine()!
    .Split()
    .Select(int.Parse)
    .ToArray();

Console.WriteLine(values.Sum());
```

### FR-064: STDIN limitations

If the browser runtime cannot provide fully interactive terminal input, the application shall clearly document that programs cannot pause and wait for new user input after execution starts.

### FR-065: Execution state

While running:

- Run shall be disabled or displayed as Running.
- Stop shall be enabled.
- A progress indicator shall be visible.
- The running program shall use its captured source snapshot.
- Editor changes shall apply only to the next run.

### FR-066: Stop action

Stop shall terminate the active worker if safe cooperative cancellation is not possible.

### FR-067: Worker recreation

After termination, the worker manager shall create and initialize a replacement worker without requiring a full page refresh.

### FR-068: Execution timeout

The default maximum execution duration shall be 5 seconds. The architecture shall support configurable limits, but Version 1 may expose only the default.

### FR-069: Infinite-loop handling

A program such as the following shall be stoppable:

```csharp
while (true)
{
}
```

### FR-070: Output limit

The application shall stop retaining output after a configured maximum. The recommended Version 1 limit is 100,000 characters.

The UI shall display:

```plaintext
Output truncated because the maximum output limit was reached.
```

### FR-071: Output batching

The worker shall batch output events to avoid a UI render for every character or small write call.

### FR-072: Runtime exceptions

Unhandled exceptions shall be shown separately from compiler diagnostics.

### FR-073: Exception presentation

The exception view shall show:

- Exception type
- Message
- Relevant user-code stack information where available
- Expandable technical details

### FR-074: Execution measurements

The result shall include:

- Compilation duration
- Execution duration
- Total duration
- Final status
- Output truncation state

Timings shall be described as browser-observed measurements, not precision benchmark results.

### FR-075: No concurrent runs

Version 1 shall allow only one active compilation/execution request. Run shall remain unavailable until the request completes, fails, stops, or times out.

---

## 10.5 Pane Layout

### FR-080: Two-pane workspace

The workspace shall contain:

- Source editor pane
- I/O pane

### FR-081: Default pane order

The editor shall initially appear on the left and I/O on the right.

### FR-082: Pane interchange

The user shall be able to switch to I/O left and editor right.

### FR-083: Draggable divider

A vertical divider shall allow resizing.

Suggested minimum widths:

- Editor: 320 pixels
- I/O: 280 pixels

### FR-084: Nested I/O divider

STDIN and Output/Problems shall use a vertically draggable divider.

### FR-085: Editor focus mode

The editor shall be expandable to fill the application workspace while retaining a clear Restore Layout action.

### FR-086: I/O focus mode

The I/O area shall be expandable to fill the application workspace.

### FR-087: Browser fullscreen

The application should expose the browser Fullscreen API where supported and permitted.

### FR-088: Internal fullscreen fallback

If browser fullscreen is unavailable, codedotnet shall provide an application focus mode that hides nonessential elements.

### FR-089: Escape behavior

Escape shall exit focus mode, fullscreen, or an open modal according to the active context.

### FR-090: Persist layout

Pane order, pane size, and internal I/O split ratio shall be persisted locally.

### FR-091: Restore default layout

A single action shall restore the default pane order and ratios.

---

## 10.6 Themes and Appearance

### FR-100: System theme

On first use, codedotnet shall follow `prefers-color-scheme`.

### FR-101: Theme choices

The user shall be able to select:

- System
- Light
- Dark

### FR-102: Monaco synchronization

Changing the product theme shall apply a corresponding Monaco theme.

### FR-103: Theme persistence

The selected theme shall be stored locally.

### FR-104: Original design system

codedotnet shall define original:

- Colors
- Typography
- Logo
- Icons or icon arrangement
- Button presentation
- Spacing and border system
- Loading experience

### FR-105: High-contrast readiness

Color tokens shall be structured so a future high-contrast theme can be added without redesigning components.

---

## 10.7 Toolbar and Commands

### FR-110: Minimum toolbar actions

The toolbar shall include:

- codedotnet logo/name
- Runtime indicator
- Run
- Stop
- Clear output
- Reset code
- Download source
- Swap panes
- Editor focus/full-screen
- I/O focus/full-screen
- Theme selector
- Settings
- About/runtime information

### FR-111: Tooltips

Icon-only actions shall have visible tooltips and accessible names.

### FR-112: Keyboard shortcuts

Minimum commands:

```plaintext
Ctrl/Cmd + Enter      Run
Shift + F5            Stop
Ctrl/Cmd + S          Save locally
Ctrl/Cmd + F          Find
Ctrl/Cmd + H          Replace
Ctrl/Cmd + Z          Undo
Ctrl/Cmd + Y          Redo
F11                   Focus or fullscreen mode
Escape                Exit active focus mode or dialog
```

Browser conflicts shall be tested and documented.

### FR-113: Command availability

Actions shall be enabled or disabled based on execution state. For example, Stop shall be unavailable when no execution is active.

---

## 10.8 Local Persistence

### FR-120: Persisted information

codedotnet may locally persist:

- Current `Program.cs`
- STDIN buffer
- Theme
- Editor preferences
- Pane order
- Pane sizes
- I/O split ratio
- Last selected Output/Problems tab

### FR-121: Prohibited persistence

Version 1 shall not create cloud records containing source or input.

### FR-122: Storage schema version

Locally stored objects shall include a schema version to support future migration.

### FR-123: Clear local data

Settings shall include a Clear Local Data action that removes codedotnet-local saved values and restores defaults.

### FR-124: Storage failure

If storage access fails, codedotnet shall continue operating for the current session and show a non-blocking warning.

---

# 11. Non-Functional Requirements

## 11.1 Performance

### NFR-P001: Responsive application shell

The primary UI shall render without waiting for all compiler resources to finish loading.

### NFR-P002: Worker-based heavy processing

Compiler and execution workloads shall run off the primary UI thread.

### NFR-P003: Lazy loading

Large runtime and compiler assets shall load separately from the smallest practical UI bundle.

### NFR-P004: Cache-friendly assets

Production runtime and compiler resources shall use immutable, versioned asset names where possible.

### NFR-P005: Warm execution

A healthy worker shall reuse initialized runtime, compiler, and metadata references across executions.

### NFR-P006: Release optimization

Production builds shall use:

- Minification
- Tree shaking
- Release-mode .NET build
- Runtime relinking or trimming where compatible
- Optimized static resources
- Compressed hosting responses where provided
- Bundle analysis
- Separate development and production source-map policy

### NFR-P007: Output-render optimization

Large output shall use bounded buffers and batched UI updates.

### NFR-P008: Performance regression checks

CI or scheduled testing should track:

- Total deployment size
- Initial JavaScript size
- Runtime asset size
- Compiler/reference size
- First load
- Warm load
- Runtime initialization
- Compilation duration

### NFR-P009: Asset budget

The project shall define and enforce practical warning/failure thresholds for artifact growth.

---

## 11.2 Reliability and Availability

### NFR-R001: Worker recovery

A terminated, failed, or timed-out worker shall be replaceable.

### NFR-R002: No silent failures

Every rejected operation shall produce a visible actionable state.

### NFR-R003: Error boundaries

Failures in noncritical React components shall not leave a blank application.

### NFR-R004: Defined execution states

The application shall use explicit states:

```plaintext
Uninitialized
Initializing
Ready
Compiling
Running
Completed
Failed
Stopping
Stopped
TimedOut
Recovering
```

### NFR-R005: Stale-message protection

Every worker request and response shall include a correlation identifier. Responses from a previous run shall not overwrite a newer run.

### NFR-R006: Runtime restart

A Restart Runtime action shall allow recovery from inconsistent worker state.

### NFR-R007: Storage recovery

Invalid or outdated local settings shall fall back to validated defaults.

---

## 11.3 Security and Privacy

### NFR-S001: No source transmission

No codedotnet-controlled network request shall transmit source code, standard input, output, or diagnostics.

### NFR-S002: No authentication

The product shall contain no identity provider, authentication token, account session, login form, or authorization flow.

### NFR-S003: Web Worker boundary

User code shall execute in a worker separated from the UI thread. Documentation shall not describe this as a perfect security boundary.

### NFR-S004: No secrets in frontend

No API keys, credentials, signing secrets, private connection strings, or confidential tokens shall be shipped in browser assets.

### NFR-S005: Content Security Policy

A restrictive Content Security Policy should be implemented to the extent compatible with GitHub Pages, Monaco, .NET WebAssembly, and worker loading.

### NFR-S006: Dependency controls

The repository shall include:

- Lockfiles
- Dependabot
- Automated dependency scanning
- Version pinning for critical dependencies
- License review

### NFR-S007: Telemetry

Version 1 should contain no third-party analytics. If analytics are introduced later, they shall never capture source, input, output, or diagnostic content.

### NFR-S008: Privacy verification

Before release, browser developer tools or automated network inspection shall verify that code and input are not transmitted.

### NFR-S009: Safe HTML rendering

Output and diagnostics shall be rendered as text, not trusted HTML.

---

## 11.4 Accessibility

### NFR-A001: Keyboard access

All primary actions shall be keyboard operable.

### NFR-A002: Accessible names

Controls shall have accessible names, roles, states, and descriptions where necessary.

### NFR-A003: Contrast

Core text, controls, focus indicators, and diagnostics should meet WCAG 2.1 AA contrast expectations.

### NFR-A004: Focus management

Dialogs shall trap focus while open and return focus to the invoking control when closed.

### NFR-A005: Status announcements

Runtime and execution statuses shall use appropriate non-disruptive screen-reader live regions.

### NFR-A006: Reduced motion

Nonessential animation shall respect `prefers-reduced-motion`.

### NFR-A007: Divider accessibility

Resizable dividers shall support keyboard resizing and expose orientation and value semantics where practical.

---

## 11.5 Browser Compatibility

### NFR-C001: Supported browsers

Version 1 shall target the current stable desktop versions of:

- Microsoft Edge
- Google Chrome
- Mozilla Firefox
- Apple Safari

### NFR-C002: Minimum supported viewport

The fully supported workspace shall target at least:

```plaintext
Width:  1024 pixels
Height: 600 pixels
```

### NFR-C003: Feature detection

The application shall detect:

- WebAssembly
- Web Workers
- Blob URLs
- Browser storage
- File download capability
- Fullscreen API availability

### NFR-C004: Unsupported environment

Unsupported browsers shall receive a clear compatibility message rather than an unexplained failure.

### NFR-C005: Mobile behavior

Small-device access may show a simplified read-only warning or stacked layout, but mobile IDE support is not an MVP acceptance condition.

---

## 11.6 Maintainability and Code Quality

### NFR-M001: Strict TypeScript

The React application shall use TypeScript strict mode.

### NFR-M002: Modular architecture

UI, settings, persistence, compiler, execution, worker communication, diagnostics, and layout shall be separate modules.

### NFR-M003: Component size discipline

Large components shall be decomposed by responsibility rather than accumulating compiler, editor, and layout logic together.

### NFR-M004: Static analysis

The project shall use:

- ESLint
- Prettier
- TypeScript compiler checks
- .NET analyzers where practical

### NFR-M005: Testability

Application logic shall use dependency boundaries that permit worker, storage, clock, and runtime behavior to be tested.

### NFR-M006: Documentation

Architecture-critical public interfaces and non-obvious decisions shall be documented.

### NFR-M007: Small change sets

Development should use small, reviewable commits, particularly when AI-assisted coding is used.

---

## 11.7 Scalability

### NFR-SC001: Client-side scaling

Each user's browser provides the CPU and memory used for compilation and execution. Increasing users shall not require central compiler instances.

### NFR-SC002: Static-distribution scaling

The primary shared resource is static asset bandwidth. Runtime and compiler caching shall minimize repeat transfer.

### NFR-SC003: Hosting-limit monitoring

Deployment size and estimated bandwidth impact shall be monitored because runtime/compiler files can be large.

---

# 12. Technical Architecture

## 12.1 Architecture style

codedotnet shall use a static single-page application with a client-side worker-based compiler and runtime.

```plaintext
GitHub Pages
     │
     │ static assets
     ▼
Browser Main Thread
     │
     ├── React UI
     ├── Monaco Editor
     ├── Layout and settings
     ├── Local persistence
     └── Execution coordinator
              │
              │ typed postMessage protocol
              ▼
        Dedicated Web Worker
              │
              ├── .NET 10 WebAssembly runtime
              ├── Roslyn compiler
              ├── Framework metadata references
              ├── In-memory assembly
              ├── Execution host
              └── Console/diagnostic capture
```

## 12.2 Recommended stack

### Frontend

- React
- TypeScript
- Vite
- Monaco Editor
- React resizable panels or an equivalent accessible library
- CSS Modules or a documented design-token approach

### Runtime and compiler

- .NET 10 WebAssembly runtime
- Roslyn C# compiler libraries
- Curated .NET reference assemblies
- JavaScript/.NET interoperability

### Worker and communication

- Dedicated Web Worker
- Structured-clone-compatible messages
- Versioned message contract
- Correlation IDs
- Worker lifecycle manager

### Persistence

- `localStorage` for source and compact settings
- IndexedDB only if required for controlled application-level caching

### Testing

- Vitest
- React Testing Library
- Playwright
- .NET test project for compiler host behavior

### CI/CD

- GitHub Actions
- GitHub Pages
- Dependabot

---

# 13. Component Architecture

## 13.1 Application Shell

Responsibilities:

- Root layout
- Global theme
- Boot status
- Error boundary
- Global modal host
- Accessibility announcements

## 13.2 Toolbar

Responsibilities:

- Runtime status
- Run and Stop
- Clear, Reset, and Download
- Pane swapping
- Focus/fullscreen actions
- Theme and settings
- About information

## 13.3 Monaco Editor Adapter

Responsibilities:

- Monaco loading
- `Program.cs` model lifecycle
- Editor preferences
- Diagnostic markers
- Error navigation
- Keyboard command registration
- Source update events
- Disposal and cleanup

## 13.4 I/O Panel

Responsibilities:

- STDIN editing
- Output rendering
- Problems list
- Execution status
- Timings
- Output clearing
- Output truncation indicator

## 13.5 Layout Manager

Responsibilities:

- Pane ratios
- Pane order
- Nested I/O ratio
- Focus modes
- Browser fullscreen
- Keyboard divider movement
- Local persistence
- Default restoration

## 13.6 Settings Service

Responsibilities:

- Default settings
- Validation
- Schema version
- Migration
- Persistence
- Reset

## 13.7 Execution Coordinator

Responsibilities:

- Execution state machine
- Immutable request creation
- Request correlation
- Run lock
- Timeout timer
- Stop
- Worker recovery
- Result aggregation

## 13.8 Worker Manager

Responsibilities:

- Worker creation
- Runtime initialization
- Message transport
- Failure detection
- Termination
- Replacement
- Stale-message rejection

## 13.9 Compiler Host

Responsibilities:

- Parse source
- Configure Roslyn
- Load and reuse references
- Emit in-memory assembly
- Convert diagnostics to a transport format

## 13.10 Execution Host

Responsibilities:

- Load generated assembly
- Provide STDIN
- Capture stdout and stderr
- Execute entry point
- Capture exceptions
- Report timings

---

# 14. Worker Communication Protocol

Messages shall use a typed, versioned envelope.

Conceptual request envelope:

```plaintext
protocolVersion
requestId
timestamp
operation
payload
```

## 14.1 Main thread to worker operations

- `InitializeRuntime`
- `CompileAndRun`
- `GetRuntimeInformation`
- `ResetWorkerState`

Stop may be implemented by terminating the worker itself instead of relying on a worker message.

## 14.2 Worker to main thread events

- `InitializationProgress`
- `RuntimeReady`
- `CompilationStarted`
- `DiagnosticsProduced`
- `ExecutionStarted`
- `OutputProduced`
- `ExecutionCompleted`
- `ExecutionFailed`
- `WorkerError`

## 14.3 Message rules

- Every run receives a unique request ID.
- Every response includes its request ID.
- UI accepts results only for the active request.
- Payloads contain plain structured data.
- Binary emitted assemblies remain inside the worker whenever possible.
- Output is batched.
- Protocol changes increment a version.

---

# 15. Execution State Model

```plaintext
UNINITIALIZED
      │
      ▼
INITIALIZING
      ├── failure ──► INITIALIZATION_FAILED
      │                    │
      │                    └── retry ──► INITIALIZING
      ▼
READY
      │ Run
      ▼
COMPILING
      ├── diagnostics with errors ──► READY
      ▼
RUNNING
      ├── success ────────────────► READY
      ├── runtime failure ────────► READY
      ├── stop ──────────────────► RECOVERING ─► READY
      ├── timeout ───────────────► RECOVERING ─► READY
      └── worker crash ──────────► RECOVERING ─► READY
```

The UI shall derive command availability from this state model rather than from unrelated booleans.

---

# 16. Data and Local Storage Design

## 16.1 Suggested storage keys

```plaintext
codedotnet.source
codedotnet.stdin
codedotnet.editor.settings
codedotnet.layout.settings
codedotnet.theme
codedotnet.storage.version
```

## 16.2 Stored-source model

The source record should conceptually contain:

```plaintext
schemaVersion
filename
content
updatedAt
```

## 16.3 Settings validation

All loaded values shall be validated against supported ranges. Invalid fields shall use defaults without preventing the application from starting.

## 16.4 No sensitive data guarantee

codedotnet shall not encourage users to paste secrets. A UI notice may state:

> Avoid entering passwords, API keys, private certificates, personal data, or production secrets into any coding playground, even when execution is local.

---

# 17. GitHub Pages Architecture

## 17.1 Static deployment

The production output shall include only static resources:

- `index.html`
- JavaScript and CSS bundles
- Monaco worker bundles
- .NET WebAssembly runtime files
- Roslyn assemblies
- Curated framework/reference assemblies
- Icons and manifest files

## 17.2 Repository path support

The build shall work under:

```plaintext
https://<username>.github.io/codedotnet/
```

Vite base paths, Web Worker URLs, Monaco worker URLs, and .NET runtime asset paths shall all respect the repository subpath.

## 17.3 Custom domains

Possible DNS arrangement:

- `codedotnet.in` as the canonical domain
- `www.codedotnet.in` redirecting to the canonical domain
- `codedotnet.com` redirecting to `codedotnet.in`, or acting as the canonical global domain
- `www.codedotnet.com` redirecting appropriately

Only one custom domain is normally configured directly for a GitHub Pages site. The second domain can use registrar or DNS-level forwarding, or a lightweight redirect service. The final choice should be documented.

## 17.4 HTTPS

GitHub Pages HTTPS enforcement shall be enabled after DNS configuration is valid.

## 17.5 Single-page routing

The MVP should avoid nested client-side public routes. Dialogs and settings should remain application states so direct refreshes do not require route fallback workarounds.

---

# 18. CI/CD Requirements

## 18.1 Pull-request validation

Every pull request shall perform:

1. Node dependency restore
2. .NET dependency restore
3. TypeScript type checking
4. ESLint
5. Formatting verification
6. Frontend unit tests
7. .NET tests
8. Compiler compatibility tests
9. Production build
10. Artifact-size check

## 18.2 Main-branch deployment

Approved changes to the main branch shall:

1. Repeat all quality checks.
2. Build the .NET WebAssembly worker.
3. Build the React frontend.
4. Verify required runtime assets.
5. Run smoke tests against the built artifact.
6. Upload the Pages artifact.
7. Deploy through GitHub Pages.
8. Report the deployment URL.

## 18.3 Required asset validation

CI shall fail if required files are missing, including:

- .NET runtime loader
- WebAssembly runtime
- Compiler host assembly
- Roslyn assemblies
- Reference assemblies
- Worker script
- Monaco worker bundle
- Main JavaScript and stylesheet

## 18.4 Artifact-size control

CI should report the total deployment size and fail when a deliberately configured upper limit is exceeded.

---

# 19. Performance Engineering Plan

## 19.1 UI shell

- Keep the initial React shell small.
- Render loading UI immediately.
- Avoid blocking startup on compiler readiness.
- Use system fonts for application chrome unless branding requires otherwise.

## 19.2 Monaco

- Load only required language support.
- Maintain a single `Program.cs` model.
- Avoid recreating the editor during React renders.
- Debounce source persistence.
- Dispose editor models and subscriptions correctly.
- Avoid using Monaco for output unless a read-only editor provides measurable value.

## 19.3 Runtime

- Publish Release builds.
- Reuse the initialized runtime inside the worker.
- Investigate runtime relinking and compatible trimming.
- Cache immutable framework files.
- Show true loading progress.

## 19.4 Roslyn

- Load references once per worker.
- Reuse parse and compilation configuration where safe.
- Curate references instead of shipping every possible assembly.
- Avoid complete IntelliSense analysis in Version 1.
- Keep emitted assembly data within the worker.

## 19.5 Output

- Batch messages by time or size.
- Use a bounded output buffer.
- Avoid expensive syntax highlighting for output.
- Virtualize output if future requirements increase the limit.

## 19.6 Measurement

Record cold and warm results for:

- UI shell load
- Monaco load
- Runtime initialization
- Roslyn initialization
- Simple compilation
- Large-source compilation
- Execution
- Worker recovery

---

# 20. Security and Privacy Design

## 20.1 Threat considerations

Even browser-only execution can encounter:

- Infinite loops
- Excessive output
- High memory use
- Worker crashes
- Browser instability
- Malicious HTML-like output
- Dependency compromise
- Unintended source transmission through telemetry

## 20.2 Controls

- Dedicated disposable worker
- Execution timeout
- Stop action
- Output limit
- Source-size limit
- Render output as text
- No backend API
- No analytics in Version 1
- Dependency scanning
- Restrictive CSP where compatible
- No secrets in bundles
- Network inspection tests

## 20.3 Privacy statement

The public application shall include language similar to:

> codedotnet processes C# source code and program input locally in the browser. The application does not require an account and does not intentionally send source code, standard input, console output, or compiler diagnostics to a remote compilation server.

The statement shall be adjusted if future telemetry or services alter actual behavior.

---

# 21. Testing Strategy

## 21.1 Unit tests

Test:

- Settings validation
- Theme logic
- Storage serialization
- Storage migrations
- Layout conversion
- State transitions
- Worker message parsing
- Correlation handling
- Diagnostic mapping
- Output batching
- Output truncation
- Source-size validation
- Timeout behavior

## 21.2 Component tests

Test:

- Toolbar command states
- Settings dialog
- Theme selector
- Runtime loading UI
- Editor marker application
- Problems navigation
- Pane swapping
- Pane focus modes
- Clear and Reset behavior
- Storage warnings
- Error boundary

## 21.3 Compiler compatibility suite

The suite shall include programs covering:

- Hello World
- Variables and primitive types
- Conditions
- Loops
- Methods
- Classes
- Interfaces
- Abstract classes
- Inheritance
- Generics
- Arrays
- Lists
- Dictionaries
- Hash sets
- LINQ
- Records
- Pattern matching
- Nullable reference usage
- Exceptions
- Recursion
- Tasks and async behavior where supported
- Binary trees
- Stacks and queues
- Priority queues
- Modern language syntax supported by the included compiler

Each case shall define:

- Source
- Input
- Expected compilation status
- Expected diagnostic codes where applicable
- Expected output
- Expected execution status

## 21.4 Negative tests

Test:

- Syntax error
- Missing symbol
- Missing type reference
- Invalid entry point
- Empty source
- Null-reference exception
- Divide-by-zero exception
- Infinite loop
- Excessive output
- Source above size limit
- Worker crash simulation
- Corrupted local settings
- Storage unavailable
- Missing runtime asset

## 21.5 End-to-end tests

Playwright shall verify:

1. Application loads from a repository base path.
2. Runtime reaches Ready.
3. Default program executes.
4. Console output appears.
5. Invalid code produces markers and Problems entries.
6. Selecting a diagnostic navigates to source.
7. Stop recovers from a long-running program.
8. Source survives refresh.
9. Theme survives refresh.
10. Pane order and ratio survive refresh.
11. Reset restores starter code.
12. Download creates `Program.cs`.
13. No application-controlled request contains source or STDIN.

## 21.6 Browser matrix

Smoke-test:

- Current Microsoft Edge
- Current Google Chrome
- Current Mozilla Firefox
- Current Apple Safari when test infrastructure is available

---

# 22. MVP Acceptance Criteria

Version 1 is accepted only when all conditions are met:

1. codedotnet loads from GitHub Pages.
2. The deployment also supports a future custom-domain configuration.
3. No login, registration, or identity UI exists.
4. Exactly one source file named `Program.cs` is available.
5. Monaco provides C#-appropriate editing and colorization.
6. The actual browser runtime identifies itself as .NET 10.
7. A basic C# program compiles and runs locally.
8. Console output is displayed.
9. Buffered STDIN works for supported console-read scenarios or its limitation is explicitly documented.
10. Invalid source returns structured Roslyn diagnostics.
11. Diagnostics appear as Monaco markers.
12. Diagnostic selection navigates to the source location.
13. Compilation and execution occur in a Web Worker.
14. Infinite loops can be stopped without refreshing the page.
15. The worker recovers after stop or timeout.
16. Editor and I/O pane positions can be exchanged.
17. Pane sizes are draggable.
18. Editor and I/O focus/full-screen modes work.
19. Light, dark, and system themes work.
20. Word wrap, font size, and tab size are configurable.
21. Source, theme, editor preferences, and layout persist locally.
22. Reset, Clear, Download, Run, and Stop work.
23. Source and output limits are enforced.
24. Automated tests pass in GitHub Actions.
25. Required production assets are validated before deployment.
26. Source and STDIN are not transmitted to a backend.
27. The product uses original codedotnet branding.
28. Architecture, privacy, limitations, testing, and deployment are documented.
29. Real performance measurements are published.
30. The application remains responsive during compile and run operations.

---

# 23. Minimum Feature Set

## 23.1 Editor minimum

- One `Program.cs`
- Monaco Editor
- C# syntax highlighting
- Line numbers
- Undo and redo
- Find and replace
- Word wrap
- Font size
- Tab size
- Light and dark themes
- Local autosave

## 23.2 Compiler and runtime minimum

- .NET 10 WebAssembly runtime
- Roslyn compilation
- Curated reference assemblies
- Web Worker execution
- Console output
- Buffered STDIN or documented limitation
- Compilation diagnostics
- Runtime error display
- Stop
- Timeout
- Output limit

## 23.3 Layout minimum

- Editor pane
- I/O pane
- Draggable divider
- Reversible pane order
- Editor focus mode
- I/O focus mode
- Restore layout

## 23.4 Quality minimum

- GitHub Pages deployment
- CI/CD
- Unit tests
- Compiler compatibility tests
- End-to-end smoke tests
- README
- Architecture document
- Privacy statement
- Known limitations
- License review

---

# 24. Delivery Plan

## Phase 0: Feasibility Spike

### Goal

Prove the hardest technical path before investing in the complete UI.

### Deliverables

- Load .NET 10 WebAssembly.
- Load Roslyn.
- Compile hard-coded C#.
- Return diagnostics.
- Execute emitted code.
- Capture `Console.WriteLine`.
- Run inside a Web Worker.
- Terminate an infinite loop.
- Validate GitHub Pages-compatible static asset paths.

### Exit criteria

A minimal static page performs the complete browser compilation and execution cycle.

### Estimated effort

1 to 2 weeks part-time.

## Phase 1: UI Foundation

### Deliverables

- codedotnet design tokens and branding
- React shell
- Monaco Editor
- Toolbar
- I/O pane
- Resizable layout
- Pane swapping
- Focus modes
- Theme support
- Settings UI

### Estimated effort

4 to 7 days.

## Phase 2: Compiler Integration

### Deliverables

- Monaco source connected to worker
- Roslyn diagnostics
- Monaco markers
- Problems list
- Diagnostic navigation
- Build status

### Estimated effort

5 to 10 days.

## Phase 3: Execution Lifecycle

### Deliverables

- Console output
- STDIN buffer
- Runtime exceptions
- Run and Stop
- Timeout
- Output limit
- Worker recreation
- Timings

### Estimated effort

5 to 8 days.

## Phase 4: Preferences and Persistence

### Deliverables

- Source autosave
- Theme persistence
- Editor settings
- Layout persistence
- Reset
- Download
- Keyboard shortcuts
- Clear local data

### Estimated effort

3 to 5 days.

## Phase 5: Quality and Optimization

### Deliverables

- Unit tests
- Component tests
- Compatibility suite
- Playwright tests
- Bundle analysis
- Runtime caching
- Accessibility review
- Cross-browser smoke tests
- Performance report

### Estimated effort

1 to 2 weeks.

## Phase 6: Release and Portfolio Documentation

### Deliverables

- GitHub Actions
- GitHub Pages release
- Domain setup documentation
- README
- Architecture diagrams
- ADRs
- Screenshots
- Demo video
- Privacy and limitations
- Resume-ready project summary

### Estimated effort

3 to 5 days.

## Overall estimate

- MVP: 5 to 7 weeks part-time
- Reliable portfolio release: 7 to 10 weeks part-time
- Focused effort: approximately 90 to 150 hours

---

# 25. Architecture Decision Records

## ADR-001: Browser execution instead of server execution

### Decision

Compile and execute C# inside the browser.

### Reasons

- Static hosting
- No compiler backend
- No account requirement
- No per-run infrastructure cost
- Reduced server attack surface
- Source-code privacy
- Natural client-side scaling

### Trade-offs

- Large initial download
- Browser memory limitations
- Restricted OS APIs
- No arbitrary NuGet restore
- Worker termination for hard cancellation

## ADR-002: React and TypeScript

### Decision

Use React and strict TypeScript.

### Reasons

- Modular component architecture
- Strong ecosystem
- Testability
- Monaco integration
- Portfolio relevance

## ADR-003: Monaco Editor

### Decision

Use Monaco Editor for `Program.cs`.

### Reasons

- Professional editor experience
- C# colorization
- Diagnostics and markers
- Configurable editor behavior
- Familiar interaction patterns

## ADR-004: Single-file MVP

### Decision

Support exactly one `Program.cs` file.

### Reasons

- Lower complexity
- Simpler compiler contract
- Faster delivery
- Fewer persistence and UI states
- Adequate for algorithm practice

## ADR-005: Dedicated Web Worker

### Decision

Run compilation and execution in a disposable worker.

### Reasons

- Responsive UI
- Infinite-loop recovery
- Clear lifecycle
- Background runtime initialization

## ADR-006: GitHub Pages

### Decision

Use GitHub Pages for initial public deployment.

### Reasons

- Static hosting
- HTTPS
- Git-based deployment
- No server maintenance
- Portfolio visibility

## ADR-007: No analytics in Version 1

### Decision

Do not include third-party analytics.

### Reasons

- Stronger privacy claim
- No accidental source capture
- Simpler CSP
- Smaller payload

## ADR-008: Buffered STDIN

### Decision

Collect the complete STDIN value before execution.

### Reasons

- Simpler browser-worker interaction
- Predictable testing
- Suitable for interview programs
- Avoids pretending to provide a fully interactive terminal

---

# 26. Risk Register

## RISK-001: Roslyn and references produce a large deployment

**Impact:** High
**Mitigation:** Curate references, track artifact size, cache immutable assets, investigate trimming, and fail CI on unexpected growth.

## RISK-002: Runtime startup is slow

**Impact:** High
**Mitigation:** Render UI early, display progress, preload carefully, and optimize warm caching.

## RISK-003: Infinite loops consume CPU

**Impact:** Critical
**Mitigation:** Use a disposable Web Worker, execution timeout, and visible Stop action.

## RISK-004: STDIN is not fully interactive

**Impact:** Medium
**Mitigation:** Define buffered STDIN, test `Console.ReadLine`, and document limitations.

## RISK-005: GitHub Pages base paths break runtime assets

**Impact:** High
**Mitigation:** Centralized base URL configuration and production-path end-to-end tests.

## RISK-006: Browser compatibility differs

**Impact:** Medium
**Mitigation:** Feature detection, compatibility page, and browser smoke tests.

## RISK-007: Product overstates full .NET compatibility

**Impact:** High
**Mitigation:** Display actual runtime data, publish compatibility tests, and document unsupported APIs.

## RISK-008: UI appears to copy OneCompiler

**Impact:** Medium
**Mitigation:** Original codedotnet branding, design tokens, logo, colors, spacing, and component arrangement.

## RISK-009: GitHub Pages bandwidth pressure

**Impact:** High
**Mitigation:** Caching, small curated runtime surface, usage monitoring, and a future alternate static host strategy.

## RISK-010: AI-assisted code is not understood

**Impact:** Medium
**Mitigation:** Small commits, tests, architecture reviews, manual explanation of every critical subsystem, and no unreviewed large rewrites.

## RISK-011: Custom domains are assumed to be free

**Impact:** Low to medium
**Mitigation:** Document that GitHub Pages hosting can be free, while `.in` and `.com` registration normally costs money.

---

# 27. Documentation Deliverables

The repository shall include:

- `README.md`
- Product overview
- Feature list
- Quick start
- Architecture diagram
- Requirements specification
- Local development guide
- Build guide
- Testing guide
- GitHub Pages deployment guide
- Custom-domain guide for `codedotnet.in` and `codedotnet.com`
- Privacy statement
- Security notes
- Browser compatibility
- Known limitations
- Performance report
- Architecture Decision Records
- Contribution guide
- Code of conduct if public collaboration is enabled
- License
- Third-party notices

---

# 28. Proposed Repository Structure

```plaintext
codedotnet/
├── .github/
│   ├── dependabot.yml
│   └── workflows/
│       ├── ci.yml
│       └── deploy-pages.yml
├── docs/
│   ├── architecture.md
│   ├── requirements.md
│   ├── performance.md
│   ├── privacy.md
│   ├── security.md
│   ├── testing.md
│   ├── deployment.md
│   ├── custom-domains.md
│   └── adr/
│       ├── 001-browser-execution.md
│       ├── 002-react-typescript.md
│       ├── 003-monaco-editor.md
│       ├── 004-single-file-mvp.md
│       ├── 005-web-worker.md
│       ├── 006-github-pages.md
│       ├── 007-no-analytics.md
│       └── 008-buffered-stdin.md
├── src/
│   ├── app/
│   ├── components/
│   │   ├── editor/
│   │   ├── io-panel/
│   │   ├── layout/
│   │   ├── settings/
│   │   ├── status-bar/
│   │   └── toolbar/
│   ├── compiler/
│   ├── execution/
│   ├── persistence/
│   ├── themes/
│   ├── types/
│   ├── utilities/
│   └── workers/
├── dotnet/
│   ├── CompilerHost/
│   └── CompilerHost.Tests/
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── compiler-cases/
│   └── e2e/
├── public/
├── LICENSE
├── NOTICE
├── README.md
├── package.json
├── tsconfig.json
└── vite.config.ts
```

> **Note:** This project's actual implementation uses `codedotnet.web/` and `codedotnet.compiler/` as top-level Visual Studio project folders instead of the `src/` and `dotnet/CompilerHost` layout shown above, to preserve clean Visual Studio solution/project semantics. This is an intentional, documented deviation from this illustrative structure.

---

# 29. Definition of Done

A requirement or feature is Done only when:

- Acceptance criteria pass.
- Appropriate automated tests exist.
- Failure states are handled.
- Keyboard operation is verified.
- Light and dark themes are verified.
- No visible main-thread regression is introduced.
- Documentation is updated.
- Production build succeeds.
- GitHub Pages subpath behavior is tested.
- No unexpected source transmission occurs.
- Accessibility checks are completed.
- The change is reviewed and understandable without relying on an AI conversation.

---

# 30. Release Readiness Checklist

## Product

- [ ] codedotnet branding is original.
- [ ] No login or registration exists.
- [ ] Single-file scope is clear.
- [ ] Known limitations are visible.

## Runtime

- [ ] Actual runtime reports .NET 10.
- [ ] Roslyn compiler version is documented.
- [ ] Compatibility suite passes.
- [ ] Worker recovery succeeds.

## Editor

- [ ] Monaco loads correctly.
- [ ] C# colorization works.
- [ ] Diagnostics map correctly.
- [ ] Word wrap and font size work.
- [ ] Themes work.

## Execution

- [ ] `Console.WriteLine` works.
- [ ] STDIN behavior is verified.
- [ ] Stop works.
- [ ] Timeout works.
- [ ] Output truncation works.
- [ ] Runtime errors are readable.

## Layout

- [ ] Pane resizing works.
- [ ] Pane interchange works.
- [ ] Editor focus mode works.
- [ ] I/O focus mode works.
- [ ] Layout persists.

## Privacy and security

- [ ] Network inspection confirms no source transmission.
- [ ] Output is rendered as text.
- [ ] No secrets are included.
- [ ] Dependency scan passes.
- [ ] Privacy statement matches implementation.

## Deployment

- [ ] GitHub Actions passes.
- [ ] GitHub Pages URL works.
- [ ] Repository-subpath assets work.
- [ ] Custom-domain instructions are documented.
- [ ] HTTPS is enforced when a custom domain is enabled.

## Quality

- [ ] Unit tests pass.
- [ ] Component tests pass.
- [ ] Compiler cases pass.
- [ ] End-to-end tests pass.
- [ ] Artifact-size budget passes.
- [ ] Performance report is updated.
- [ ] Accessibility review is complete.

---

# 31. Future Roadmap

The following items are candidates only after Version 1 is stable:

## Version 1.1

- Better runtime loading progress
- Command palette
- More editor preferences
- Offline Progressive Web App support
- Improved diagnostic filtering
- Additional starter templates

## Version 1.2

- Shareable compressed code in the URL, subject to URL-size and privacy analysis
- Local execution history
- Export settings
- Import source file
- Additional accessibility improvements

## Version 2

- Multiple source files
- Virtual file explorer
- Project export
- Selected package support under a controlled model
- Richer IntelliSense
- Syntax-tree or IL visualization

Authentication, cloud sync, collaboration, and server execution should remain separate architectural decisions rather than being assumed future additions.

---

# 32. Portfolio Positioning

Recommended project description:

> codedotnet is a privacy-first, single-file C# development environment that compiles and executes modern C# entirely inside the browser using Roslyn and the .NET 10 WebAssembly runtime. The application uses Web Worker isolation to keep the editor responsive and recover from long-running programs, requires no account or backend compiler service, and deploys as static assets through GitHub Pages.

Recommended engineering discussion topics:

- Why browser execution was selected over server execution
- How Roslyn builds syntax trees and emits assemblies
- How framework references are loaded
- How diagnostics map to Monaco markers
- How output and STDIN are redirected
- Why Web Worker termination is used for hard cancellation
- How GitHub Pages subpath deployment affects asset loading
- How runtime/compiler assets are optimized and cached
- How privacy claims are verified
- Which APIs differ from desktop .NET
- How real performance data was collected

---

# 33. Reference Documentation

The implementation team should validate technical details against current official documentation during development:

- Microsoft Learn: .NET on Web Workers
  `https://learn.microsoft.com/aspnet/core/client-side/dotnet-on-webworkers`

- Microsoft Learn: ASP.NET Core Blazor with .NET on Web Workers
  `https://learn.microsoft.com/aspnet/core/blazor/blazor-with-dotnet-on-web-workers`

- Microsoft Learn: Host and deploy Blazor WebAssembly
  `https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/`

- Microsoft Learn: WebAssembly build tools and AOT
  `https://learn.microsoft.com/aspnet/core/blazor/webassembly-build-tools-and-aot`

- Microsoft Learn: WebAssembly runtime performance
  `https://learn.microsoft.com/aspnet/core/blazor/performance/webassembly-runtime-performance`

- Monaco Editor
  `https://microsoft.github.io/monaco-editor/`

- Monaco Editor API
  `https://microsoft.github.io/monaco-editor/typedoc/`

- GitHub Pages documentation
  `https://docs.github.com/pages`

- GitHub Pages limits
  `https://docs.github.com/pages/getting-started-with-github-pages/github-pages-limits`

---

# 34. Final Approved Product Statement

> **codedotnet** is a clean, modern, single-file C# playground that uses a bundled .NET 10 WebAssembly runtime and Roslyn compiler to compile and execute source locally inside a desktop browser. codedotnet requires no authentication and no backend compiler service, protects UI responsiveness through Web Worker execution, provides configurable editor and pane behavior, and can be deployed as a static GitHub Pages application under its repository URL or the custom domains `codedotnet.in` and `codedotnet.com`.

The first and most important milestone is the feasibility spike: reliable Roslyn compilation, browser-local execution, output capture, diagnostic mapping, and worker recovery. The polished codedotnet interface should be built only after this technical path is proven.
