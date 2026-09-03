import {
  PROTOCOL_VERSION,
  type CompileAndRunResponsePayload,
  type RuntimeInformation,
  type WorkerRequestEnvelope,
  type WorkerResponseEnvelope,
} from './protocol'

/** Default maximum execution duration in milliseconds (FR-068). */
export const DEFAULT_EXECUTION_TIMEOUT_MS = 5_000

export type WorkerManagerState =
  | 'Uninitialized'
  | 'Initializing'
  | 'Ready'
  | 'Busy'
  | 'Stopping'
  | 'Stopped'
  | 'Failed'
  | 'Recovering'

type PendingRequest = {
  resolve: (value: unknown) => void
  reject: (reason: Error) => void
}

/**
 * Owns the lifecycle of the dedicated .NET 10 WebAssembly Web Worker: creation,
 * runtime initialization tracking, correlated request/response messaging, stale-response
 * rejection, termination, and replacement after Stop/timeout/crash.
 */
export class WorkerManager {
  private worker: Worker | null = null
  private state: WorkerManagerState = 'Uninitialized'
  private nextRequestId = 0
  private activeRequestId: number | null = null
  private readonly pending = new Map<number, PendingRequest>()
  private readonly listeners = new Set<(state: WorkerManagerState) => void>()

  getState(): WorkerManagerState {
    return this.state
  }

  onStateChange(listener: (state: WorkerManagerState) => void): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  private setState(next: WorkerManagerState): void {
    this.state = next
    for (const listener of this.listeners) {
      listener(next)
    }
  }

  async initialize(): Promise<void> {
    this.setState('Initializing')

    return new Promise<void>((resolve, reject) => {
      // Intentionally a plain string path (not `new URL(..., import.meta.url)`): worker.js is a
      // pre-built static asset published by codedotnet.compiler and served from public/, so it
      // must NOT be picked up and bundled by Vite's module-worker static analysis.
      const workerUrl = `${import.meta.env.BASE_URL}dotnet-worker/wwwroot/worker.js`
      const worker = new Worker(workerUrl, { type: 'module' })

      const handleMessage = (event: MessageEvent<WorkerResponseEnvelope>): void => {
        const message = event.data

        if (message.operation === 'RuntimeReady') {
          this.setState('Ready')
          resolve()
          return
        }

        if (message.operation === 'RuntimeError') {
          this.setState('Failed')
          reject(new Error(message.error ?? 'Unknown worker initialization error'))
          return
        }

        if (message.operation === 'Response') {
          this.handleResponse(message)
        }
      }

      worker.addEventListener('message', handleMessage)
      worker.addEventListener('error', (event) => {
        this.setState('Failed')
        reject(new Error(event.message))
      })

      this.worker = worker
    })
  }

  private handleResponse(message: WorkerResponseEnvelope): void {
    if (message.requestId === null || message.requestId !== this.activeRequestId) {
      // Stale or unrelated response: discard silently per NFR-R005.
      return
    }

    const request = this.pending.get(message.requestId)
    this.pending.delete(message.requestId)
    this.activeRequestId = null
    this.setState('Ready')

    if (!request) {
      return
    }

    if (message.error) {
      request.reject(new Error(message.error))
      return
    }

    request.resolve(message.payload)
  }

  async getRuntimeInformation(): Promise<RuntimeInformation> {
    return this.send('GetRuntimeInformation') as Promise<RuntimeInformation>
  }

  /**
   * Compiles and runs the provided source snapshot (FR-040/FR-075). Only one active
   * compile/run request is allowed at a time; the worker is hard-terminated and replaced
   * if the execution exceeds `timeoutMs` (FR-066 through FR-069).
   */
  async compileAndRun(
    source: string,
    stdin: string,
    timeoutMs: number = DEFAULT_EXECUTION_TIMEOUT_MS,
  ): Promise<CompileAndRunResponsePayload> {
    const requestPromise = this.send('CompileAndRun', { source, stdin }) as Promise<CompileAndRunResponsePayload>

    const timeoutPromise = new Promise<CompileAndRunResponsePayload>((_resolve, reject) => {
      setTimeout(() => {
        this.stop()
        void this.initialize()
        reject(new Error('Execution timed out'))
      }, timeoutMs)
    })

    return Promise.race([requestPromise, timeoutPromise])
  }

  private send<TPayload = undefined>(
    operation: WorkerRequestEnvelope['operation'],
    payload?: TPayload,
  ): Promise<unknown> {
    if (!this.worker || this.state !== 'Ready') {
      return Promise.reject(new Error(`Worker is not ready (state: ${this.state})`))
    }

    const requestId = ++this.nextRequestId
    this.activeRequestId = requestId
    this.setState('Busy')

    const promise = new Promise<unknown>((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject })
    })

    const request: WorkerRequestEnvelope<TPayload> = {
      protocolVersion: PROTOCOL_VERSION,
      requestId,
      operation,
      payload,
    }

    this.worker.postMessage(request)

    return promise
  }

  /** Hard-cancels the active operation by terminating the worker (FR-066/FR-067). */
  stop(): void {
    if (!this.worker) {
      return
    }

    this.setState('Stopping')
    this.worker.terminate()
    this.worker = null

    for (const request of this.pending.values()) {
      request.reject(new Error('Worker stopped'))
    }
    this.pending.clear()
    this.activeRequestId = null
    this.setState('Stopped')
  }

  /** Recreates and re-initializes the worker after Stop, timeout, or crash (NFR-R001/R006). */
  async recover(): Promise<void> {
    this.setState('Recovering')
    this.stop()
    await this.initialize()
  }
}
