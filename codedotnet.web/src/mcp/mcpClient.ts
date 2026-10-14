// Minimal JSON-RPC 2.0-over-WebSocket client for talking to the codedotnet-sandbox MCP
// server (src/wsServer.mjs) from the browser. Mirrors the request/response correlation
// pattern used by WorkerManager (see ../workers/workerManager.ts), but over a WebSocket
// instead of a Worker's postMessage channel.

export type McpConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Error'

export interface McpTool {
  name: string
  description: string
  inputSchema: unknown
}

export interface McpToolCallResult {
  content: Array<{ type: string; text: string }>
  isError: boolean
}

interface JsonRpcResponse {
  jsonrpc: '2.0'
  id: number | null
  result?: unknown
  error?: { code: number; message: string }
}

type PendingRequest = {
  resolve: (value: unknown) => void
  reject: (reason: Error) => void
}

/**
 * Owns a single WebSocket connection to a codedotnet-sandbox MCP server, correlating
 * JSON-RPC requests/responses by id and exposing a small typed surface for the tools
 * this playground needs (initialize, tools/list, tools/call).
 */
export class McpClient {
  private socket: WebSocket | null = null
  private state: McpConnectionState = 'Disconnected'
  private nextRequestId = 0
  private readonly pending = new Map<number, PendingRequest>()
  private readonly listeners = new Set<(state: McpConnectionState) => void>()

  getState(): McpConnectionState {
    return this.state
  }

  onStateChange(listener: (state: McpConnectionState) => void): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  private setState(next: McpConnectionState): void {
    this.state = next
    for (const listener of this.listeners) {
      listener(next)
    }
  }

  connect(url: string): Promise<void> {
    this.disconnect()
    this.setState('Connecting')

    return new Promise<void>((resolve, reject) => {
      let socket: WebSocket
      try {
        socket = new WebSocket(url)
      } catch (err) {
        this.setState('Error')
        reject(err instanceof Error ? err : new Error(String(err)))
        return
      }

      this.socket = socket

      socket.addEventListener('open', () => {
        this.setState('Connected')
        resolve()
      })

      socket.addEventListener('message', (event: MessageEvent<string>) => {
        let message: JsonRpcResponse
        try {
          message = JSON.parse(event.data)
        } catch {
          return
        }

        if (message.id === null || message.id === undefined) {
          return
        }

        const pendingRequest = this.pending.get(message.id)
        if (!pendingRequest) {
          return
        }
        this.pending.delete(message.id)

        if (message.error) {
          pendingRequest.reject(new Error(message.error.message))
        } else {
          pendingRequest.resolve(message.result)
        }
      })

      socket.addEventListener('close', () => {
        this.setState('Disconnected')
        this.rejectAllPending(new Error('WebSocket connection closed'))
      })

      socket.addEventListener('error', () => {
        this.setState('Error')
        reject(new Error('Failed to connect to MCP server'))
        this.rejectAllPending(new Error('WebSocket connection error'))
      })
    })
  }

  disconnect(): void {
    if (this.socket) {
      this.socket.close()
      this.socket = null
    }
    this.rejectAllPending(new Error('Disconnected'))
    this.setState('Disconnected')
  }

  private rejectAllPending(reason: Error): void {
    for (const pendingRequest of this.pending.values()) {
      pendingRequest.reject(reason)
    }
    this.pending.clear()
  }

  private call(method: string, params: unknown): Promise<unknown> {
    if (!this.socket || this.state !== 'Connected') {
      return Promise.reject(new Error('Not connected to an MCP server'))
    }

    const id = ++this.nextRequestId
    const promise = new Promise<unknown>((resolve, reject) => {
      this.pending.set(id, { resolve, reject })
    })

    this.socket.send(JSON.stringify({ jsonrpc: '2.0', id, method, params }))
    return promise
  }

  async initialize(): Promise<{ serverInfo: { name: string; version: string } }> {
    return (await this.call('initialize', {})) as { serverInfo: { name: string; version: string } }
  }

  async listTools(): Promise<McpTool[]> {
    const result = (await this.call('tools/list', {})) as { tools: McpTool[] }
    return result.tools
  }

  async callTool(name: string, args: Record<string, unknown>): Promise<McpToolCallResult> {
    return (await this.call('tools/call', { name, arguments: args })) as McpToolCallResult
  }
}
