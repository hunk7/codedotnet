# Multi-stage build for the codedotnet-sandbox MCP WebSocket server (src/wsServer.mjs).
#
# Why this exists: codedotnet-sandbox/src/worker.mjs boots the published CodeDotNet.Engine
# browser-wasm artifact from codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js
# (see codedotnet.compiler/codedotnet.compiler.csproj's <PublishDir>). Render's native Node
# build image has no .NET SDK, so stage 1 uses the official .NET 10 SDK image to publish that
# artifact, and stage 2 is a slim Node runtime that only needs the published static assets
# plus the sandbox's own JS sources - it never needs the SDK itself. This mirrors the two-step
# process .github/workflows/deploy.yml already performs for the GitHub Pages build
# (dotnet publish, then npm install/build), just packaged as a single deployable image.
#
# Build context MUST be the repository root (not codedotnet-sandbox/), since this Dockerfile
# needs codedotnet.compiler/, CodeDotNet.Engine/, and CodeDotNet.Contracts/ as well as
# codedotnet-sandbox/ itself. See render.yaml's dockerContext setting.

# ---- Stage 1: publish the browser-wasm worker artifact ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS wasm-build
WORKDIR /src

# Copy only the project files needed to publish codedotnet.compiler, preserving the relative
# <ProjectReference> layout (CodeDotNet.Contracts, CodeDotNet.Engine) it depends on.
COPY CodeDotNet.Contracts/ ./CodeDotNet.Contracts/
COPY CodeDotNet.Engine/ ./CodeDotNet.Engine/
COPY codedotnet.compiler/ ./codedotnet.compiler/

# codedotnet.compiler.csproj's <PublishDir>../codedotnet.web/public/dotnet-worker</PublishDir>
# is relative to the project file, so the output lands at /src/codedotnet.web/public/dotnet-worker
# even though codedotnet.web itself is never copied into this stage.
RUN dotnet workload install wasm-tools \
    && dotnet publish codedotnet.compiler/codedotnet.compiler.csproj -c Release

# ---- Stage 2: runtime image - Node only, no .NET SDK ----
FROM node:22-slim AS runtime
WORKDIR /app

# Install sandbox dependencies first for better layer caching.
COPY codedotnet-sandbox/package*.json ./codedotnet-sandbox/
RUN cd codedotnet-sandbox && npm ci --omit=dev

# Copy the sandbox source (stdio + WebSocket transports, worker pool, shared mcpCore dispatch).
COPY codedotnet-sandbox/src ./codedotnet-sandbox/src
COPY codedotnet-sandbox/mcp.json ./codedotnet-sandbox/mcp.json

# Copy only the published wasm artifact from stage 1, at the exact relative path
# worker.mjs imports from: ../../codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js
COPY --from=wasm-build /src/codedotnet.web/public/dotnet-worker ./codedotnet.web/public/dotnet-worker

ENV NODE_ENV=production
# Render injects PORT at runtime; HOST must be 0.0.0.0 to accept external connections
# (the wsServer.mjs default of 127.0.0.1 only works for local-loopback dev).
ENV HOST=0.0.0.0

EXPOSE 7900

WORKDIR /app/codedotnet-sandbox
CMD ["node", "src/wsServer.mjs"]
