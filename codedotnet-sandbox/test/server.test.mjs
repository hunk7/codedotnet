// End-to-end smoke test: spawns the real codedotnet-sandbox stdio server as a child
// process, sends MCP-style JSON-RPC requests, and verifies compile_and_run works plus
// that an infinite loop is hard-killed within the configured timeout. Requires the
// browser-wasm artifact to already be published to
// ../../codedotnet.web/public/dotnet-worker (run `dotnet publish` on codedotnet.compiler
// first).

import { test } from 'node:test'
import assert from 'node:assert/strict'
import { fork } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const SERVER_PATH = path.join(__dirname, '..', 'src', 'server.mjs')

function startServer() {
  const child = fork(SERVER_PATH, { stdio: ['pipe', 'pipe', 'pipe', 'ipc'] })
  let buffer = ''
  const pendingResolvers = new Map()
  let nextId = 0

  child.stdout.on('data', (chunk) => {
    buffer += chunk.toString()
    let newlineIndex
    while ((newlineIndex = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, newlineIndex)
      buffer = buffer.slice(newlineIndex + 1)
      if (!line.trim()) continue
      const message = JSON.parse(line)
      const resolver = pendingResolvers.get(message.id)
      if (resolver) {
        pendingResolvers.delete(message.id)
        resolver(message)
      }
    }
  })

  function call(method, params) {
    const id = ++nextId
    const promise = new Promise((resolve) => pendingResolvers.set(id, resolve))
    child.stdin.write(JSON.stringify({ jsonrpc: '2.0', id, method, params }) + '\n')
    return promise
  }

  return { child, call }
}

test('codedotnet-sandbox: tools/list exposes compile_and_run', async () => {
  const { child, call } = startServer()
  try {
    const response = await call('tools/list', {})
    assert.ok(response.result.tools.some((t) => t.name === 'compile_and_run'))
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox: compile_and_run executes a simple program', async () => {
  const { child, call } = startServer()
  try {
    const response = await call('tools/call', {
      name: 'compile_and_run',
      arguments: { source: 'using System;\nConsole.WriteLine("hello sandbox");' },
    })

    assert.equal(response.result.isError, false)
    const payload = JSON.parse(response.result.content[0].text)
    assert.equal(payload.status, 'ExecutionCompleted')
    assert.match(payload.output, /hello sandbox/)
  } finally {
    child.kill('SIGKILL')
  }
})

test('codedotnet-sandbox: compile_and_run hard-kills an infinite loop within the timeout', async () => {
  const { child, call } = startServer()
  try {
    const start = Date.now()
    const response = await call('tools/call', {
      name: 'compile_and_run',
      arguments: {
        source: 'while (true) { }',
        timeoutMs: 2000,
      },
    })
    const elapsed = Date.now() - start

    const payload = JSON.parse(response.result.content[0].text)
    assert.equal(payload.status, 'TimedOut')
    assert.equal(payload.timedOut, true)
    assert.ok(elapsed < 5000, `expected hard-kill well under 5s, took ${elapsed}ms`)
  } finally {
    child.kill('SIGKILL')
  }
})
