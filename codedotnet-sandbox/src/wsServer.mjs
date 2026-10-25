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
import {
  DEFAULT_MAX_PAYLOAD_BYTES,
  DEFAULT_MAX_TIMEOUT_MS,
  MAX_POOL_SIZE,
  isOriginAllowed,
  parseAllowedOrigins,
  parsePositiveInt,
} from './security.mjs'

const PORT = Number(process.env.PORT ?? 7900)
const HOST = process.env.HOST ?? '127.0.0.1'
// Default-deny: browser clients may connect only from loopback origins (local dev servers).
// Clients that send no Origin header (scripts, MCP hosts) are allowed. Set MCP_ALLOWED_ORIGIN
// to a comma-separated list of exact origins (for example the hosted playground), or '*' to
// explicitly disable the check.
const ALLOWED_ORIGINS = parseAllowedOrigins(process.env.MCP_ALLOWED_ORIGIN)
const POOL_SIZE = parsePositiveInt(process.env.MCP_POOL_SIZE, undefined, MAX_POOL_SIZE)
const MAX_TIMEOUT_MS = parsePositiveInt(process.env.MCP_MAX_TIMEOUT_MS, DEFAULT_MAX_TIMEOUT_MS)
const MAX_PAYLOAD_BYTES = parsePositiveInt(
  process.env.MCP_MAX_PAYLOAD_BYTES,
  DEFAULT_MAX_PAYLOAD_BYTES,
)

const pool = new WorkerPool(POOL_SIZE === undefined ? {} : { poolSize: POOL_SIZE })
const handleRequest = createRequestHandler(pool, {
  maxTimeoutMs: MAX_TIMEOUT_MS,
  onShutdown: () => {
    pool.close()
    process.exit(0)
  },
})

const wss = new WebSocketServer({
  host: HOST,
  port: PORT,
  maxPayload: MAX_PAYLOAD_BYTES,
  verifyClient: (info) => isOriginAllowed(info.origin, ALLOWED_ORIGINS),
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
