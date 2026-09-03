import {
  PROTOCOL_VERSION,
  type RuntimeInformation,
  type WorkerRequestEnvelope,
  type WorkerResponseEnvelope,
} from './protocol'

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

  private send(operation: WorkerRequestEnvelope['operation']): Promise<unknown> {
    if (!this.worker || this.state !== 'Ready') {
      return Promise.reject(new Error(`Worker is not ready (state: ${this.state})`))
    }

    const requestId = ++this.nextRequestId
    this.activeRequestId = requestId
    this.setState('Busy')

    const promise = new Promise<unknown>((resolve, reject) => {
      this.pending.set(requestId, { resolve, reject })
    })

    const request: WorkerRequestEnvelope = {
      protocolVersion: PROTOCOL_VERSION,
      requestId,
      operation,
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
