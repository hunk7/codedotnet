# codedotnet.web

The React + TypeScript + Vite frontend for codedotnet, a browser-based C# playground. See the root README and the docs/ folder for architecture, testing, deployment, security, and privacy documentation.

## Local development

```powershell
npm install
npm run dev
```

## Common scripts

- npm run dev - Start the Vite dev server.
- npm run build - Type-check and build for production.
- npm run typecheck - Type-check without emitting.
- npm run lint - Run ESLint.
- npm test / test:watch - Run the Vitest suite once / in watch mode.
- npm run build:worker / build:worker:release - Publish the codedotnet.compiler worker and copy assets.

See docs/testing.md and docs/deployment.md for details.
