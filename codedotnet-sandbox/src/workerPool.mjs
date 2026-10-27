// Worker pool manager: owns a small pool of pre-warmed child-process workers (each
// hosting its own instance of the CodeDotNet.Engine wasm runtime) and enforces hard
// per-request timeouts via SIGKILL, per the Level 0 spike findings
// (docs/research/SPIKE_FINDINGS.md): Promise.race cannot preempt a synchronous infinite
// loop running inside the wasm runtime, so only OS-level process termination works.

import { fork } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { EventEmitter } from 'node:events'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const WORKER_SCRIPT = path.join(__dirname, 'worker.mjs')

const DEFAULT_POOL_SIZE = 2
const DEFAULT_TIMEOUT_MS = 10_000

class PooledWorker extends EventEmitter {
  constructor() {
    super()
    this.ready = false
    this.busy = false
    this.child = fork(WORKER_SCRIPT, { stdio: ['ignore', 'pipe', 'pipe', 'ipc'] })
    this.child.on('message', (message) => this.handleMessage(message))
    this.child.on('exit', (code, signal) => this.emit('exit', { code, signal }))

    this.readyPromise = new Promise((resolve, reject) => {
      this._resolveReady = resolve
      this._rejectReady = reject
    })
  }

  handleMessage(message) {
    if (!message || typeof message !== 'object') {
      return
    }

    if (message.type === 'ready') {
      this.ready = true
      this._resolveReady()
      return
    }

    if (message.type === 'boot-error') {
      this._rejectReady(new Error(message.error))
      return
    }

    if (message.type === 'result') {
      this.emit('result', message)
    }
  }

  waitUntilReady() {
    return this.readyPromise
  }

  kill() {
    try {
      this.child.kill('SIGKILL')
    } catch {
      // Process may have already exited; nothing to do.
    }
  }
}

export class WorkerPool {
  constructor({ poolSize = DEFAULT_POOL_SIZE, defaultTimeoutMs = DEFAULT_TIMEOUT_MS } = {}) {
    this.poolSize = poolSize
    this.defaultTimeoutMs = defaultTimeoutMs
    this.idleWorkers = []
    this.nextRequestId = 0
    this.closed = false

    for (let i = 0; i < poolSize; i++) {
      this.spawnAndQueue()
    }
  }

  spawnAndQueue() {
    const worker = new PooledWorker()
    worker.waitUntilReady().then(
      () => {
        if (!this.closed) {
          this.idleWorkers.push(worker)
        } else {
          worker.kill()
        }
      },
      () => {
        // Boot failed; replace it so the pool stays at full strength.
        if (!this.closed) {
          this.spawnAndQueue()
        }
      },
    )
    return worker
  }

  async acquireWorker() {
    while (this.idleWorkers.length === 0) {
      // Simple backoff poll; pool is small and workers boot in ~1-2s.
      await new Promise((resolve) => setTimeout(resolve, 25))
      if (this.closed) {
        throw new Error('Worker pool is closed')
      }
    }
    return this.idleWorkers.shift()
  }

  /**
   * Runs a single compile_and_run request against a pooled worker, enforcing a hard
   * timeout via SIGKILL. Resolves with either the engine's CompileAndRunResponse JSON
   * payload, or a synthetic timed-out response if the worker did not respond in time.
   */
  async compileAndRun({ source, timeoutMs = this.defaultTimeoutMs }) {
    if (this.closed) {
      throw new Error('Worker pool is closed')
    }

    const worker = await this.acquireWorker()
    const requestId = ++this.nextRequestId

    return new Promise((resolve) => {
      let settled = false

      const timeoutHandle = setTimeout(() => {
        if (settled) return
        settled = true
        worker.kill()
        resolve({
          status: 'TimedOut',
          timedOut: true,
          diagnostics: [],
          output: '',
          outputTruncated: false,
          exitCode: null,
          exceptionType: null,
          exceptionMessage: `Execution exceeded the ${timeoutMs}ms hard timeout and was terminated.`,
          exceptionStackTrace: null,
          compilationDurationMs: 0,
          executionDurationMs: timeoutMs,
          totalDurationMs: timeoutMs,
        })
        // Replace the killed worker so the pool stays at full strength.
        if (!this.closed) {
          this.spawnAndQueue()
        }
      }, timeoutMs)

      const onResult = (message) => {
        if (message.requestId !== requestId || settled) {
          return
        }
        settled = true
        clearTimeout(timeoutHandle)
        worker.off('result', onResult)
        worker.off('exit', onExit)

        if (message.error) {
          resolve({
            status: 'ExecutionFailed',
            timedOut: false,
            diagnostics: [],
            output: '',
            outputTruncated: false,
            exitCode: null,
            exceptionType: null,
            exceptionMessage: message.error,
            exceptionStackTrace: null,
            compilationDurationMs: 0,
            executionDurationMs: 0,
            totalDurationMs: 0,
          })
        } else {
          resolve({ ...message.payload, timedOut: false })
        }

        // This worker has served one request; it keeps running and goes back into the
        // idle pool rather than being torn down, since only the request that actually
        // hangs should ever be killed.
        if (!this.closed) {
          this.idleWorkers.push(worker)
        } else {
          worker.kill()
        }
      }

      const onExit = () => {
        if (settled) return
        settled = true
        clearTimeout(timeoutHandle)
        worker.off('result', onResult)
        resolve({
          status: 'ExecutionFailed',
          timedOut: false,
          diagnostics: [],
          output: '',
          outputTruncated: false,
          exitCode: null,
          exceptionType: null,
          exceptionMessage: 'Worker process exited unexpectedly.',
          exceptionStackTrace: null,
          compilationDurationMs: 0,
          executionDurationMs: 0,
          totalDurationMs: 0,
        })
        if (!this.closed) {
          this.spawnAndQueue()
        }
      }

      worker.on('result', onResult)
      worker.on('exit', onExit)
      worker.child.send({ type: 'compile_and_run', requestId, source })
    })
  }

  close() {
    this.closed = true
    for (const worker of this.idleWorkers) {
      worker.kill()
    }
    this.idleWorkers = []
  }
}
