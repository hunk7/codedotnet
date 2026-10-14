#!/usr/bin/env node
// Minimal stdio MCP-style JSON-RPC 2.0 server exposing a single `compile_and_run` tool.
//
// This implements only the subset of the Model Context Protocol needed for a client to
// discover and invoke the tool (initialize, tools/list, tools/call), framed as
// newline-delimited JSON-RPC messages over stdin/stdout. It intentionally has zero
// third-party dependencies so it can run without network/package-registry access.

import readline from 'node:readline'
import { WorkerPool } from './workerPool.mjs'
import { createRequestHandler } from './mcpCore.mjs'

const pool = new WorkerPool()

function send(message) {
  process.stdout.write(JSON.stringify(message) + '\n')
}

function sendError(id, code, message) {
  send({ jsonrpc: '2.0', id, error: { code, message } })
}

const handleRequest = createRequestHandler(pool, {
  onShutdown: () => {
    pool.close()
    process.exit(0)
  },
})

const rl = readline.createInterface({ input: process.stdin, terminal: false })

rl.on('line', (line) => {
  const trimmed = line.trim()
  if (!trimmed) {
    return
  }

  let request
  try {
    request = JSON.parse(trimmed)
  } catch {
    sendError(null, -32700, 'Parse error: invalid JSON')
    return
  }

  void handleRequest(request, send)
})

process.on('SIGINT', () => {
  pool.close()
  process.exit(0)
})
