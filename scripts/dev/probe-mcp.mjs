#!/usr/bin/env node
// Probes the local sandbox MCP WebSocket server: tools/list, then a tiny compile_and_run.
// Exit code 0 = healthy, 1 = failure. Usage: node scripts/dev/probe-mcp.mjs [ws://127.0.0.1:7900]

import { createRequire } from 'node:module'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const require = createRequire(path.join(here, '..', '..', 'codedotnet-sandbox', 'package.json'))
const WebSocket = require('ws')

const url = process.argv[2] ?? 'ws://127.0.0.1:7900'
const TIMEOUT_MS = 30_000

function fail(message) {
  console.error(`FAIL: ${message}`)
  process.exit(1)
}

const timer = setTimeout(() => fail(`timed out after ${TIMEOUT_MS} ms`), TIMEOUT_MS)
const ws = new WebSocket(url)
let nextId = 1
const pending = new Map()

function call(method, params) {
  const id = nextId++
  return new Promise((resolve) => {
    pending.set(id, resolve)
    ws.send(JSON.stringify({ jsonrpc: '2.0', id, method, params }))
  })
}

ws.on('error', (err) => fail(`cannot connect to ${url}: ${err.message}`))
ws.on('message', (data) => {
  const message = JSON.parse(data.toString())
  const resolve = pending.get(message.id)
  if (resolve) {
    pending.delete(message.id)
    resolve(message)
  }
})

ws.on('open', async () => {
  const list = await call('tools/list', {})
  if (!list.result?.tools?.some((t) => t.name === 'compile_and_run')) {
    fail('tools/list did not include compile_and_run')
  }
  console.log('ok: tools/list includes compile_and_run')

  const run = await call('tools/call', {
    name: 'compile_and_run',
    arguments: { source: 'System.Console.WriteLine("probe-ok");', timeoutMs: 20000 },
  })
  if (run.error) {
    fail(`tools/call error: ${run.error.message}`)
  }
  const payload = JSON.parse(run.result.content[0].text)
  if (payload.status !== 'ExecutionCompleted' || !/probe-ok/.test(payload.output)) {
    fail(`unexpected compile_and_run result: ${JSON.stringify(payload)}`)
  }
  console.log('ok: compile_and_run executed')

  clearTimeout(timer)
  ws.close()
  process.exit(0)
})
