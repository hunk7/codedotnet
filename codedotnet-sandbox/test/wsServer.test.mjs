// End-to-end smoke test for the WebSocket MCP transport: spawns wsServer.mjs as a child
// process on an ephemeral port, connects with a plain `ws` client, and verifies
// tools/list, a successful compile_and_run, and the hard-timeout path all behave
// identically to the stdio transport (test/server.test.mjs). Requires the browser-wasm
// artifact to already be published (see server.test.mjs header comment).

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { fork } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import WebSocket from 'ws'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SERVER_PATH = path.join(__dirname, '..', 'src', 'wsServer.mjs')

function startServer() {
  const port = 20000 + Math.floor(Math.random() * 20000)
  const child = fork(SERVER_PATH, {
    env: { ...process.env, PORT: String(port), HOST: '127.0.0.1' },
    stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
  })

  const ready = new Promise((resolve) => {
    child.stdout.on('data', (chunk) => {
      if (chunk.toString().includes('listening')) {
        resolve()
      }
    })
  })

  return { child, port, ready }
}

function connect(port) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`ws://127.0.0.1:${port}`)
    ws.on('open', () => resolve(ws))
    ws.on('error', reject)
  })
}

function call(ws, method, params) {
  return new Promise((resolve) => {
    const id = Math.floor(Math.random() * 1_000_000)
    function onMessage(data) {
      const message = JSON.parse(data.toString())
      if (message.id === id) {
        ws.off('message', onMessage)
        resolve(message)
      }
    }
    ws.on('message', onMessage)
    ws.send(JSON.stringify({ jsonrpc: '2.0', id, method, params }))
  })
}

test('codedotnet-sandbox wsServer: tools/list exposes compile_and_run', async () => {
  const { child, port, ready } = startServer()
  try {
    await ready
    const ws = await connect(port)
    const response = await call(ws, 'tools/list', {})
    assert.ok(response.result.tools.some((t) => t.name === 'compile_and_run'))
    ws.close()
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox wsServer: compile_and_run executes a simple program', async () => {
  const { child, port, ready } = startServer()
  try {
    await ready
    const ws = await connect(port)
    const response = await call(ws, 'tools/call', {
      name: 'compile_and_run',
      arguments: { source: 'using System;\nConsole.WriteLine("hello websocket sandbox");' },
    })

    assert.equal(response.result.isError, false)
    const payload = JSON.parse(response.result.content[0].text)
    assert.equal(payload.status, 'ExecutionCompleted')
    assert.match(payload.output, /hello websocket sandbox/)
    ws.close()
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox wsServer: compile_and_run hard-kills an infinite loop within the timeout', async () => {
  const { child, port, ready } = startServer()
  try {
    await ready
    const ws = await connect(port)
    const start = Date.now()
    const response = await call(ws, 'tools/call', {
      name: 'compile_and_run',
      arguments: { source: 'while (true) { }', timeoutMs: 2000 },
    })
    const elapsed = Date.now() - start

    const payload = JSON.parse(response.result.content[0].text)
    assert.equal(payload.status, 'TimedOut')
    assert.equal(payload.timedOut, true)
    assert.ok(elapsed < 5000, `expected hard-kill well under 5s, took ${elapsed}ms`)
    ws.close()
  } finally {
    child.kill('SIGKILL')
  }
})
