// Temporary Level 0 technical spike (part 2): validate that a hard timeout can be
// enforced by terminating the child process hosting the wasm runtime while it is
// stuck in a synchronous infinite loop, since Promise.race cannot preempt
// synchronous execution on the same event loop/thread.
//
// Usage: node scripts/spike-node-child-timeout.mjs
// This script is NOT part of the shipped product; temporary evidence gathering only.

import { fork } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const childPath = path.join(__dirname, 'spike-child-infinite-loop.mjs')

function nowMs() {
  return Number(process.hrtime.bigint()) / 1e6
}

const HARD_TIMEOUT_MS = 2000

const t0 = nowMs()
const child = fork(childPath, { stdio: ['ignore', 'pipe', 'pipe', 'ipc'] })

let killedByTimeout = false
let exited = false

child.stdout.on('data', (d) => console.log(`[child stdout] ${d.toString().trim()}`))
child.stderr.on('data', (d) => console.log(`[child stderr] ${d.toString().trim()}`))

const timer = setTimeout(() => {
  if (!exited) {
    killedByTimeout = true
    console.log(`[spike] hard timeout reached at ${(nowMs() - t0).toFixed(1)}ms, killing child pid=${child.pid}`)
    child.kill('SIGKILL')
  }
}, HARD_TIMEOUT_MS)

child.on('exit', (code, signal) => {
  exited = true
  clearTimeout(timer)
  const elapsed = nowMs() - t0
  console.log(`[spike] child exited code=${code} signal=${signal} elapsedMs=${elapsed.toFixed(1)} killedByTimeout=${killedByTimeout}`)

  const ok = killedByTimeout && signal === 'SIGKILL' && elapsed < HARD_TIMEOUT_MS + 1000
  console.log('\n=== CHILD TIMEOUT SPIKE RESULT ===')
  console.log(JSON.stringify({ ok, code, signal, elapsedMs: elapsed, killedByTimeout }, null, 2))
  process.exit(ok ? 0 : 1)
})
