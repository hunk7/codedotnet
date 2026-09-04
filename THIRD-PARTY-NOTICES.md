# Third-Party Notices

codedotnet is licensed under the MIT License (see [`LICENSE`](LICENSE)). It builds on the
following third-party software, redistributed or bundled as part of the built application or
its build tooling. This file summarizes their licenses; it is not a substitute for reviewing the
full license text of each project.

## Runtime / bundled in the built application

- **Monaco Editor** (`monaco-editor` npm package) — MIT License.
  https://github.com/microsoft/monaco-editor
- **.NET Runtime / .NET WebAssembly (`browser-wasm`) build** — MIT License.
  https://github.com/dotnet/runtime
- **Microsoft.CodeAnalysis.CSharp (Roslyn)** — MIT License.
  https://github.com/dotnet/roslyn
- **React** (`react`, `react-dom`) — MIT License.
  https://github.com/facebook/react

## Build / development tooling (not redistributed in the built application)

- **Vite** — MIT License. https://github.com/vitejs/vite
- **Vitest** — MIT License. https://github.com/vitest-dev/vitest
- **TypeScript** — Apache License 2.0. https://github.com/microsoft/TypeScript
- **@testing-library/react**, **@testing-library/jest-dom** — MIT License.
  https://github.com/testing-library
- **xUnit.net** — Apache License 2.0. https://github.com/xunit/xunit
- **Microsoft.NET.Test.Sdk**, **coverlet.collector** — MIT License.

## Notes

- All bundled runtime dependencies above use the MIT License, which is compatible with this
  project's MIT License and imposes no copyleft obligations.
- Dependency versions are tracked in `codedotnet.web/package.json` and the `.csproj` files under
  `codedotnet.compiler*`; automated update pull requests are configured via
  [`.github/dependabot.yml`](.github/dependabot.yml) (see [`docs/security.md`](docs/security.md)).
- If you redistribute a built copy of codedotnet, you must retain the copyright and license
  notices of the bundled third-party components listed above, per their respective license
  terms.
