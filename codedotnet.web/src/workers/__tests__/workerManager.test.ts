import { describe, it, expect, beforeEach, afterEach } from 'vitest'
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
    const hungRequest = manager.compileAndRun('while (true) {}', '', DEFAULT_EXECUTION_TIMEOUT_MS * 10)
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

    const hungRequest = manager.compileAndRun('while (true) {}', '', DEFAULT_EXECUTION_TIMEOUT_MS * 10)
    manager.stop()

    await expect(hungRequest).rejects.toThrow()
  })
})
