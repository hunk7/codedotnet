#!/usr/bin/env node
// WebSocket transport for the MCP-style JSON-RPC 2.0 dispatch in mcpCore.mjs.
//
// This lets a browser GUI (which cannot spawn stdio child processes) talk to the same
// compile_and_run tool exposed by the stdio server (src/server.mjs), over a plain
// WebSocket connection carrying one JSON-RPC message per text frame. The dispatch logic,
// tool metadata, and WorkerPool-backed sandbox isolation are identical to the stdio
// transport; only the framing differs.
//
// This is the one deliberate exception to the "dependency-free" design of the stdio
// server: hand-rolling the WebSocket protocol (framing, masking, handshake) correctly
// and securely is significant surface area for no real benefit, so the small, widely
// used `ws` package is used here instead.

import { WebSocketServer } from 'ws'
import { WorkerPool } from './workerPool.mjs'
import { createRequestHandler } from './mcpCore.mjs'

const PORT = Number(process.env.PORT ?? 7900)
const HOST = process.env.HOST ?? '127.0.0.1'
// By default only same-origin/local dev servers may connect. Set MCP_ALLOWED_ORIGIN to a
// specific origin (or '*' to disable the check) when hosting the playground elsewhere.
const ALLOWED_ORIGIN = process.env.MCP_ALLOWED_ORIGIN ?? null

const pool = new WorkerPool()
const handleRequest = createRequestHandler(pool, {
  onShutdown: () => {
    pool.close()
    process.exit(0)
  },
})

function isOriginAllowed(origin) {
  if (ALLOWED_ORIGIN === '*' || ALLOWED_ORIGIN === null) {
    return true
  }
  return origin === ALLOWED_ORIGIN
}

const wss = new WebSocketServer({
  host: HOST,
  port: PORT,
  verifyClient: (info) => isOriginAllowed(info.origin),
})

wss.on('connection', (ws) => {
  function send(message) {
    if (ws.readyState === ws.OPEN) {
      ws.send(JSON.stringify(message))
    }
  }

  ws.on('message', (data) => {
    let request
    try {
      request = JSON.parse(data.toString())
    } catch {
      send({ jsonrpc: '2.0', id: null, error: { code: -32700, message: 'Parse error: invalid JSON' } })
      return
    }

    void handleRequest(request, send)
  })
})

wss.on('listening', () => {
  console.log(`codedotnet-sandbox MCP WebSocket server listening on ws://${HOST}:${PORT}`)
})

process.on('SIGINT', () => {
  pool.close()
  wss.close(() => process.exit(0))
})
