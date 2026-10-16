import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { WorkerManager, DEFAULT_EXECUTION_TIMEOUT_MS } from '../workerManager'
import { PROTOCOL_VERSION, type WorkerResponseEnvelope } from '../protocol'

/**
 * Minimal fake Worker that mimics a .NET worker which never responds to a
 * CompileAndRun request (simulating a user program stuck in an infinite loop, e.g.
 * `while (true) {}`). This lets us prove that WorkerManager.stop()/recover() can hard-cancel
 * a hung request and restore a healthy `Ready` worker without waiting for a real timeout
 * (FR-066/FR-067/FR-069, NFR-R001/NFR-R006).
 */
class HangingWorker implements Partial<Worker> {
  static instances: HangingWorker[] = []
  terminated = false
  private listeners: Record<string, ((event: MessageEvent<WorkerResponseEnvelope>) => void)[]> = {}

  constructor() {
    HangingWorker.instances.push(this)
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    const fn = listener as (event: MessageEvent<WorkerResponseEnvelope>) => void
    this.listeners[type] ??= []
    this.listeners[type].push(fn)

    // Immediately signal RuntimeReady on message-listener registration so initialize() resolves.
    if (type === 'message') {
      queueMicrotask(() => {
        fn({
          data: { protocolVersion: PROTOCOL_VERSION, requestId: null, operation: 'RuntimeReady' },
        } as MessageEvent<WorkerResponseEnvelope>)
      })
    }
  }

  removeEventListener(): void {}

  postMessage(): void {
    // Intentionally never responds: simulates a hung/infinite-loop execution request.
  }

  terminate(): void {
    this.terminated = true
  }
}

describe('WorkerManager stop/recover after a hung execution', () => {
  const originalWorker = globalThis.Worker

  beforeEach(() => {
    HangingWorker.instances = []
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    globalThis.Worker = HangingWorker as any
  })

  afterEach(() => {
    globalThis.Worker = originalWorker
  })

  it('terminates a hung worker via stop() and returns to Ready after recover()', async () => {
    const manager = new WorkerManager()

    await manager.initialize()
    expect(manager.getState()).toBe('Ready')

    // Fire a CompileAndRun request that will never resolve (simulated infinite loop).
    const hungRequest = manager.compileAndRun(
      'while (true) {}',
      '',
      DEFAULT_EXECUTION_TIMEOUT_MS * 10,
    )
    // Swallow the rejection triggered by stop(); we assert on state transitions instead.
    hungRequest.catch(() => {})

    expect(manager.getState()).toBe('Busy')

    const firstWorker = HangingWorker.instances[0]
    expect(firstWorker.terminated).toBe(false)

    manager.stop()

    expect(firstWorker.terminated).toBe(true)
    expect(manager.getState()).toBe('Stopped')

    await manager.recover()

    expect(manager.getState()).toBe('Ready')
    expect(HangingWorker.instances.length).toBe(2)
    expect(HangingWorker.instances[1].terminated).toBe(false)
  })

  it('rejects any pending request when stop() is called', async () => {
    const manager = new WorkerManager()
    await manager.initialize()

    const hungRequest = manager.compileAndRun(
      'while (true) {}',
      '',
      DEFAULT_EXECUTION_TIMEOUT_MS * 10,
    )
    manager.stop()

    await expect(hungRequest).rejects.toThrow()
  })
})

/**
 * Fake Worker that responds successfully to CompileAndRun immediately, used to verify the
 * timeout handle is cleared after a successful response (regression for a leaked
 * setTimeout that fired later and spuriously called stop()/initialize() on an unrelated,
 * already-idle worker).
 */
class RespondingWorker implements Partial<Worker> {
  static instances: RespondingWorker[] = []
  terminated = false
  private listeners: Record<string, ((event: MessageEvent<WorkerResponseEnvelope>) => void)[]> = {}

  constructor() {
    RespondingWorker.instances.push(this)
  }

  addEventListener(type: string, listener: EventListenerOrEventListenerObject): void {
    const fn = listener as (event: MessageEvent<WorkerResponseEnvelope>) => void
    this.listeners[type] ??= []
    this.listeners[type].push(fn)

    if (type === 'message') {
      queueMicrotask(() => {
        fn({
          data: { protocolVersion: PROTOCOL_VERSION, requestId: null, operation: 'RuntimeReady' },
        } as MessageEvent<WorkerResponseEnvelope>)
      })
    }
  }

  removeEventListener(): void {}

  postMessage(message: unknown): void {
    const { requestId } = message as { requestId: number }
    queueMicrotask(() => {
      for (const fn of this.listeners['message'] ?? []) {
        fn({
          data: {
            protocolVersion: PROTOCOL_VERSION,
            requestId,
            operation: 'Response',
            payload: { status: 'ExecutionCompleted' },
          },
        } as unknown as MessageEvent<WorkerResponseEnvelope>)
      }
    })
  }

  terminate(): void {
    this.terminated = true
  }
}

describe('WorkerManager compileAndRun timeout handle cleanup', () => {
  const originalWorker = globalThis.Worker

  beforeEach(() => {
    RespondingWorker.instances = []
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    globalThis.Worker = RespondingWorker as any
  })

  afterEach(() => {
    globalThis.Worker = originalWorker
    vi.useRealTimers()
  })

  it('clears the timeout after a successful response, so it never fires on a later worker', async () => {
    vi.useFakeTimers()
    const manager = new WorkerManager()

    const initializePromise = manager.initialize()
    await vi.runAllTimersAsync()
    await initializePromise

    const runPromise = manager.compileAndRun(
      'Console.WriteLine(1);',
      '',
      DEFAULT_EXECUTION_TIMEOUT_MS,
    )
    await vi.runAllTimersAsync()
    await runPromise

    expect(manager.getState()).toBe('Ready')
    const firstWorker = RespondingWorker.instances[0]

    // Advance time well past the original timeout. If the timeout handle had leaked, this
    // would call stop()/initialize(), terminating the still-healthy worker and creating a
    // second instance even though nothing is pending anymore.
    await vi.advanceTimersByTimeAsync(DEFAULT_EXECUTION_TIMEOUT_MS * 2)

    expect(firstWorker.terminated).toBe(false)
    expect(manager.getState()).toBe('Ready')
    expect(RespondingWorker.instances.length).toBe(1)
  })
})
