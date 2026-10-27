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

function startServer(extraEnv = {}) {
  const port = 20000 + Math.floor(Math.random() * 20000)
  const child = fork(SERVER_PATH, {
    env: { ...process.env, PORT: String(port), HOST: '127.0.0.1', ...extraEnv },
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

function connect(port, options) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`ws://127.0.0.1:${port}`, options)
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

test('codedotnet-sandbox wsServer: rejects a non-loopback browser Origin by default', async () => {
  const { child, port, ready } = startServer()
  try {
    await ready
    await assert.rejects(connect(port, { origin: 'https://evil.example' }))
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox wsServer: allows a loopback Origin by default', async () => {
  const { child, port, ready } = startServer()
  try {
    await ready
    const ws = await connect(port, { origin: 'http://localhost:5173' })
    ws.close()
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox wsServer: MCP_ALLOWED_ORIGIN list admits only listed origins', async () => {
  const { child, port, ready } = startServer({ MCP_ALLOWED_ORIGIN: 'https://hunk7.github.io' })
  try {
    await ready
    const ws = await connect(port, { origin: 'https://hunk7.github.io' })
    ws.close()
    await assert.rejects(connect(port, { origin: 'http://localhost:5173' }))
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox wsServer: oversized frame closes the connection', async () => {
  const { child, port, ready } = startServer({ MCP_MAX_PAYLOAD_BYTES: '2048' })
  try {
    await ready
    const ws = await connect(port)
    const closed = new Promise((resolve) => ws.on('close', (code) => resolve(code)))
    ws.send('x'.repeat(10_000))
    assert.equal(await closed, 1009)
  } finally {
    child.kill('SIGKILL')
  }
})
