import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { McpClient } from '../mcpClient'

/**
 * Minimal fake WebSocket implementing just enough of the browser WebSocket surface for
 * McpClient: open/message/close/error event listeners and a send() spy. Lets tests
 * simulate server responses deterministically without a real network connection,
 * mirroring the HangingWorker/RespondingWorker fakes used for WorkerManager tests.
 */
class FakeWebSocket {
  static instances: FakeWebSocket[] = []
  sent: string[] = []
  url: string
  private listeners: Record<string, ((event: unknown) => void)[]> = {}

  constructor(url: string) {
    this.url = url
    FakeWebSocket.instances.push(this)
  }

  addEventListener(type: string, listener: (event: unknown) => void): void {
    this.listeners[type] ??= []
    this.listeners[type].push(listener)
  }

  removeEventListener(): void {}

  send(data: string): void {
    this.sent.push(data)
  }

  close(): void {
    this.emit('close', {})
  }

  emit(type: string, event: unknown): void {
    for (const listener of this.listeners[type] ?? []) {
      listener(event)
    }
  }

  simulateOpen(): void {
    this.emit('open', {})
  }

  simulateMessage(data: unknown): void {
    this.emit('message', { data: JSON.stringify(data) })
  }
}

describe('McpClient', () => {
  const originalWebSocket = globalThis.WebSocket

  beforeEach(() => {
    FakeWebSocket.instances = []
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    globalThis.WebSocket = FakeWebSocket as any
  })

  afterEach(() => {
    globalThis.WebSocket = originalWebSocket
  })

  it('transitions through Connecting -> Connected on a successful connect()', async () => {
    const client = new McpClient()
    const states: string[] = []
    client.onStateChange((s) => states.push(s))

    const connectPromise = client.connect('ws://localhost:7900')
    expect(client.getState()).toBe('Connecting')

    FakeWebSocket.instances[0].simulateOpen()
    await connectPromise

    expect(client.getState()).toBe('Connected')
    expect(states).toEqual(['Disconnected', 'Connecting', 'Connected'])
  })

  it('correlates tools/list request and response by id', async () => {
    const client = new McpClient()
    const connectPromise = client.connect('ws://localhost:7900')
    FakeWebSocket.instances[0].simulateOpen()
    await connectPromise

    const listPromise = client.listTools()
    const sentMessage = JSON.parse(FakeWebSocket.instances[0].sent[0])
    expect(sentMessage.method).toBe('tools/list')

    FakeWebSocket.instances[0].simulateMessage({
      jsonrpc: '2.0',
      id: sentMessage.id,
      result: { tools: [{ name: 'compile_and_run', description: 'x', inputSchema: {} }] },
    })

    const tools = await listPromise
    expect(tools).toHaveLength(1)
    expect(tools[0].name).toBe('compile_and_run')
  })

  it('rejects the callTool promise when the server returns a JSON-RPC error', async () => {
    const client = new McpClient()
    const connectPromise = client.connect('ws://localhost:7900')
    FakeWebSocket.instances[0].simulateOpen()
    await connectPromise

    const callPromise = client.callTool('compile_and_run', { source: 'bad' })
    const sentMessage = JSON.parse(FakeWebSocket.instances[0].sent[0])

    FakeWebSocket.instances[0].simulateMessage({
      jsonrpc: '2.0',
      id: sentMessage.id,
      error: { code: -32602, message: 'Missing required string argument' },
    })

    await expect(callPromise).rejects.toThrow('Missing required string argument')
  })

  it('rejects all pending requests when the connection closes', async () => {
    const client = new McpClient()
    const connectPromise = client.connect('ws://localhost:7900')
    FakeWebSocket.instances[0].simulateOpen()
    await connectPromise

    const callPromise = client.callTool('compile_and_run', { source: 'x' })
    FakeWebSocket.instances[0].close()

    await expect(callPromise).rejects.toThrow()
    expect(client.getState()).toBe('Disconnected')
  })

  it('rejects calls made before a connection is established', async () => {
    const client = new McpClient()
    await expect(client.listTools()).rejects.toThrow('Not connected')
  })
})
