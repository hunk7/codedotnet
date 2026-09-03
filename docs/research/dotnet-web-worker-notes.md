# Research Notes: Microsoft `DotNetOnWebWorkersReact` Sample

**Source:** https://github.com/dotnet/blazor-samples/tree/main/10.0/DotNetOnWebWorkersReact
**Fetched via:** `raw.githubusercontent.com` (the GitHub tree UI only renders navigation chrome through the page-fetch tool; the GitHub Contents API + raw file URLs were used instead).
**Purpose:** Inform the design of `codedotnet.compiler` (the .NET 10 browser-wasm worker project) and `codedotnet.web/src/workers/compiler.worker.ts` for M1/M2 feasibility.

---

## 1. Repository layout of the sample

```
DotNetOnWebWorkersReact/
├── README.md
├── dotnet/
│   ├── Program.cs
│   ├── QRGenerator.csproj
│   └── wwwroot/
│       └── worker.js
└── react/
    ├── package.json
    ├── public/
    └── src/
        ├── client.js
        ├── QRImage.js
        ├── Popup.js
        ├── index.js
        └── index.css
```

## 2. .NET project (`dotnet/QRGenerator.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk.WebAssembly">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <WasmMainJSPath>main.js</WasmMainJSPath>
    <OutputType>Exe</OutputType>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PublishDir>../react/dist/dotnet</PublishDir>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>
    <InvariantGlobalization>true</InvariantGlobalization>
    <InvariantTimezone>true</InvariantTimezone>
    <WasmEnableExceptionHandling>false</WasmEnableExceptionHandling>
    <BlazorWebAssemblyJiterpreter>false</BlazorWebAssemblyJiterpreter>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="QRCoder" Version="1.4.3" />
  </ItemGroup>
</Project>
```

Key takeaways for `codedotnet.compiler`:

- SDK is `Microsoft.NET.Sdk.WebAssembly`, `RuntimeIdentifier` is `browser-wasm`, `OutputType` is `Exe` (a runnable wasm app, not a class library).
- `WasmMainJSPath` designates a JS entry file — **note**: the sample's `dotnet/` folder does not contain a `main.js`; it appears the entry file requirement is satisfied differently or the referenced file is generated/optional in this SDK version. This should be re-verified against current `Microsoft.NET.Sdk.WebAssembly` docs when building `codedotnet.compiler`, since our worker (`worker.js` equivalent) directly imports `./_framework/dotnet.js` and does not seem to depend on `main.js` at runtime for worker-hosted scenarios.
- `PublishDir` redirects the publish output directly into the React app's `dist/dotnet` — this is the pattern `codedotnet`'s asset-copy script should follow conceptually (publish once, integrate via a copy step), though codedotnet targets `public/dotnet-worker/` instead of a `dist/` folder to keep dev-server and Vite build behavior consistent.
- Trimming (`PublishTrimmed`, `TrimMode=full`), `InvariantGlobalization`, `InvariantTimezone`, disabling the Jiterpreter, and disabling wasm exception handling are all aggressive size/perf optimizations appropriate for **Release** publishes — worth adopting for `codedotnet.compiler` once past the feasibility stage (NFR-P006), but not necessarily during initial M1 feasibility work where debuggability matters more.

## 3. Exported C# method pattern (`dotnet/Program.cs`)

```csharp
using System.Runtime.InteropServices.JavaScript;

public partial class QRGenerator
{
    [JSExport]
    internal static byte[] Generate(string text, int qrSize)
    {
        // ...
    }
}
```

Takeaway: methods callable from JS are marked `[JSExport]` on a `partial class`, using `System.Runtime.InteropServices.JavaScript`. `codedotnet.compiler`'s runtime-information method (M1) should follow the same pattern — a static `[JSExport]` method on a partial class returning a JSON string (simplest structured-clone-safe payload) containing `Environment.Version`, `RuntimeInformation.FrameworkDescription`, and architecture.

## 4. Worker bootstrap (`dotnet/wwwroot/worker.js`)

```javascript
import { dotnet } from './_framework/dotnet.js'

let assemblyExports = null;
let startupError = undefined;

try {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const config = getConfig();
  assemblyExports = await getAssemblyExports(config.mainAssemblyName);
}
catch (err) {
  startupError = err.message;
}

self.addEventListener('message', async function(e) {
  try {
    if (!assemblyExports) {
      throw new Error(startupError || "worker exports not loaded");
    }
    let result = null;
    switch (e.data.command) {
      case "generateQR":
        result = assemblyExports.QRGenerator.Generate(e.data.text, Number(e.data.size));
        break;
      default:
        throw new Error("Unknown command: " + e.data.command);
    }
    self.postMessage({ command: "response", requestId: e.data.requestId, result });
  }
  catch (err) {
    self.postMessage({ command: "response", requestId: e.data.requestId, error: err.message });
  }
}, false);
```

Key architectural facts confirmed for `codedotnet.web/src/workers/compiler.worker.ts`:

- **Runtime entry point**: `dotnet.create()` from the published `_framework/dotnet.js`, called once at worker top-level (module-scope `await`), not per-request. This directly validates our NFR-P005 "warm execution" requirement — the runtime is created exactly once when the worker module loads, and `assemblyExports` is cached in a worker-scope variable for reuse across all subsequent messages.
- **Worker entry point**: a plain ES module Worker (`{ type: "module" }`), loaded relative to the React app's public assets — for codedotnet this will be `codedotnet.web/public/dotnet-worker/_framework/dotnet.js` after the asset-copy step, referenced from `codedotnet.web/src/workers/compiler.worker.ts`.
- **Message pattern**: a flat `{ command, requestId, ...payload }` request object and `{ command: "response", requestId, result | error }` response object. Every request must carry its own `requestId` so the main thread can correlate responses (this directly matches our NFR-R005 stale-message-rejection requirement — we should keep the `requestId` field name/shape consistent with this proven pattern but wrap it in our richer typed envelope from Section 14 of the architecture spec, i.e., `{ protocolVersion, requestId, timestamp, operation, payload }`).
- **Error handling**: startup errors are captured once (`startupError`) and surfaced on every subsequent message attempt rather than crashing the worker — this is the pattern to follow for our `RuntimeError` event and Retry action (FR-004).
- **Calling exported methods**: `assemblyExports.<ClassName>.<MethodName>(...)` — nested by class name automatically from `getAssemblyExports`.

## 5. Main-thread client wrapper (`react/src/client.js`)

```javascript
const pendingRequests = {};
let pendingRequestId = 0;

const dotnetWorker = new Worker('../../qr/wwwroot/worker.js', { type: "module" });
dotnetWorker.addEventListener('message', async function (e) {
  switch (e.data.command) {
    case "response":
      const request = pendingRequests[e.data.requestId];
      delete pendingRequests[e.data.requestId];
      if (e.data.error) { request.reject(new Error(e.data.error)); }
      request.resolve(e.data.result);
      break;
  }
}, false);

function sendRequestToWorker(request) {
  pendingRequestId++;
  const promise = new Promise((resolve, reject) => {
    pendingRequests[pendingRequestId] = { resolve, reject };
  });
  dotnetWorker.postMessage({ ...request, requestId: pendingRequestId });
  return promise;
}

export async function generateQR(text, size) {
  const response = await sendRequestToWorker({ command: "generateQR", text, size });
  // ...
}
```

Takeaway: a simple incrementing `pendingRequestId` counter plus a `{ resolve, reject }` map is sufficient to correlate promise-based requests to worker responses. This is a good starting point for `codedotnet.web/src/execution/workerManager.ts`, extended with:

- A typed envelope (protocol version, operation name, timestamp) instead of a loose `command` string, per Section 14 of the architecture spec.
- Explicit rejection of responses whose `requestId` no longer matches the "active" request (NFR-R005), not just a lookup-and-delete (the sample doesn't need this because QR generation calls are independent/idempotent, but codedotnet's Stop/replace-worker flow requires it).
- Support for worker *termination* (`Worker.terminate()`) and replacement, which this sample does not need since QR generation is fast and bounded.

## 6. Build integration and publish/copy pattern (`react/package.json` scripts)

```json
"integrate": "rm -rf $PWD/public/qr & cp -r dist/dotnet $PWD/public/qr",
"integrateWin": "rmdir %INIT_CWD%\\public\\qr /s /q & xcopy dist\\dotnet %INIT_CWD%\\public\\qr /s /e /i",
"build:dotnet": "dotnet publish -c Release ../dotnet/QRGenerator.csproj",
"build:dotnet:debug": "dotnet publish -c Debug ../dotnet/QRGenerator.csproj",
"build:all:debug": "npm run build:dotnet:debug & react-scripts build & npm run integrate"
```

Takeaway: the sample publishes .NET first (to `PublishDir` = `../react/dist/dotnet`), then copies (`cp -r` / `xcopy`) that published output into `react/public/qr`, clearing any prior copy first. This validates the M1 plan step of writing a `scripts/copy-worker-assets.(ps1|mjs)` that:

1. Runs `dotnet publish -c Debug|Release codedotnet.compiler/codedotnet.compiler.csproj`.
2. Removes any prior contents of `codedotnet.web/public/dotnet-worker/`.
3. Copies the published output into `codedotnet.web/public/dotnet-worker/`.

Since our workspace uses PowerShell as the preferred shell (per IDE state), we should implement this as a `.ps1` script (with an npm script wrapper calling `pwsh`/`powershell`) rather than duplicating Windows/Linux `cmd` variants like the sample does.

## 7. Limitations discovered

- **`main.js` / `WasmMainJSPath` ambiguity**: the `.csproj` declares `<WasmMainJSPath>main.js</WasmMainJSPath>`, but no `main.js` file exists anywhere in the sample's `dotnet/` folder. The actual worker entry point used at runtime is `wwwroot/worker.js`, which is served/copied as a static asset and directly imports `./_framework/dotnet.js`. This suggests `WasmMainJSPath` may only matter for a *non-worker* (main-thread) hosting scenario, or may be effectively unused when the app is exclusively loaded inside a Worker. **This needs to be re-validated hands-on when scaffolding `codedotnet.compiler`** — if `dotnet publish` fails or behaves unexpectedly without a `main.js` present, one may need to be added (even a minimal one) to satisfy the SDK's build targets.
- **React version mismatch**: the sample uses Create React App (`react-scripts`) and React 18, not Vite — codedotnet uses Vite + React 19, so the `integrate`/`build` script wiring will need to be adapted (Vite's `public/` folder semantics are different from CRA's, though both copy static assets verbatim into the final bundle).
- **No worker lifecycle/termination handling**: the sample's worker is long-lived and stateless per-request; it does not demonstrate `Worker.terminate()`, replacement, or stale-response rejection. M2's lifecycle/recovery work has no direct precedent in this sample and must be designed from the architecture spec's NFR-R001/R005/R006 requirements directly.
- **No correlation-ID staleness protection**: the sample's `pendingRequests` map assumes every request eventually gets exactly one response and does not need to discard "stale" responses from a previous, superseded operation. codedotnet's Stop/Recovery flow requires this to be added deliberately.
- **Trimming/AOT flags not yet validated for Roslyn**: the aggressive trimming settings (`TrimMode=full`, `InvariantGlobalization`) shown here are proven for a small QR-code library, but Roslyn's compiler assemblies are much larger and rely on reflection-heavy code paths; these settings will need dedicated compatibility testing once Roslyn is added at M3 and should not be assumed to work unmodified.

## 8. Exit criterion check

> "The developer can explain the full path from a button click in React to a C# method running inside the Web Worker and returning a result."

Confirmed end-to-end path from this research:

1. React main thread calls an exported JS function (e.g. `generateQR(text, size)`) from `client.js`.
2. `client.js` posts a `{ command, ...payload, requestId }` message to a `Worker` instance that was created pointing at `worker.js`.
3. `worker.js` (already initialized once at module load via `dotnet.create()`) receives the message, looks up the exported .NET method via `assemblyExports.<Class>.<Method>(...)`, and invokes it synchronously inside the worker.
4. The .NET method (marked `[JSExport]`) executes compiled WebAssembly code and returns a JS-interop-friendly value (e.g. `byte[]` marshaled to a JS typed array).
5. `worker.js` posts `{ command: "response", requestId, result }` back to the main thread.
6. `client.js`'s message listener resolves the pending Promise keyed by `requestId`, returning control to the original React caller.

This full path is now understood and forms the direct basis for `codedotnet.web/src/workers/compiler.worker.ts` (worker entry point) and `codedotnet.web/src/execution/workerManager.ts` (main-thread client), to be extended with codedotnet's typed envelope, correlation-ID staleness protection, and lifecycle/termination support.
