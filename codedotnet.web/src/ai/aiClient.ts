// Minimal client for the local CodeDotNet.Ai service (loopback HTTP + SSE). The browser never talks to
// Ollama or Qdrant directly. The bearer token lives in memory only: it is never written to storage.

export const DEFAULT_AI_URL = 'http://127.0.0.1:7901/v1'

export type AiConnectionState = 'Disconnected' | 'Connecting' | 'Paired' | 'Error'

export interface AiHealthCheck {
  name: string
  status: string
  detail?: string | null
}

export interface AiHealth {
  status: 'ok' | 'degraded' | 'unavailable'
  checks: AiHealthCheck[]
}

export interface AiCitation {
  label: number
  chunkId: string
  source: string
  title: string
  heading: string
}

export type AiStreamEvent =
  | { type: 'token'; text: string }
  | { type: 'citation'; citation: AiCitation }
  | { type: 'done'; data: Record<string, unknown> }
  | { type: 'evidence'; data: Record<string, unknown> }
  | { type: 'attempt'; data: Record<string, unknown> }
  | { type: 'result'; code: string; unverified: boolean }
  | { type: 'error'; code: string; message: string }

export type AiWorkflow = 'review' | 'debug' | 'tests'

export interface AiCodeRequest {
  source: string
  stdin?: string
  run?: boolean
  focus?: string
}

export class AiApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string, message: string) {
    super(message)
    this.name = 'AiApiError'
    this.status = status
    this.code = code
  }
}

/** True for loopback http(s) URLs only. The client refuses to send a token anywhere else. */
export function isLoopbackUrl(value: string): boolean {
  try {
    const url = new URL(value)
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return false
    return url.hostname === '127.0.0.1' || url.hostname === 'localhost' || url.hostname === '[::1]'
  } catch {
    return false
  }
}

/** Parses one SSE block ("event: x\ndata: {...}") into a typed event, or null when it is not understood. */
export function parseSseBlock(block: string): AiStreamEvent | null {
  let name = ''
  const dataLines: string[] = []
  for (const rawLine of block.split('\n')) {
    const line = rawLine.endsWith('\r') ? rawLine.slice(0, -1) : rawLine
    if (line.startsWith('event:')) name = line.slice(6).trim()
    else if (line.startsWith('data:')) dataLines.push(line.slice(5).trimStart())
  }
  if (!name || dataLines.length === 0) return null

  let data: unknown
  try {
    data = JSON.parse(dataLines.join('\n'))
  } catch {
    return null
  }
  if (typeof data !== 'object' || data === null) return null
  const record = data as Record<string, unknown>

  switch (name) {
    case 'token':
      return typeof record.text === 'string' ? { type: 'token', text: record.text } : null
    case 'citation':
      return { type: 'citation', citation: record as unknown as AiCitation }
    case 'done':
      return { type: 'done', data: record }
    case 'evidence':
      return { type: 'evidence', data: record }
    case 'attempt':
      return { type: 'attempt', data: record }
    case 'result':
      return typeof record.code === 'string'
        ? { type: 'result', code: record.code, unverified: record.unverified === true }
        : null
    case 'error':
      return {
        type: 'error',
        code: typeof record.code === 'string' ? record.code : 'error',
        message: typeof record.message === 'string' ? record.message : 'Unknown error',
      }
    default:
      return null
  }
}

/** Splits a streamed body into SSE events, handling blocks that arrive split across chunks. */
export async function* readSse(body: ReadableStream<Uint8Array>): AsyncGenerator<AiStreamEvent> {
  const reader = body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  try {
    for (;;) {
      const { done, value } = await reader.read()
      if (done) break
      buffer += decoder.decode(value, { stream: true }).replace(/\r\n/g, '\n')
      let index: number
      while ((index = buffer.indexOf('\n\n')) >= 0) {
        const event = parseSseBlock(buffer.slice(0, index))
        buffer = buffer.slice(index + 2)
        if (event) yield event
      }
    }
    buffer += decoder.decode()
    const tail = buffer.trim() ? parseSseBlock(buffer) : null
    if (tail) yield tail
  } finally {
    reader.releaseLock()
  }
}

export class AiClient {
  private token: string | null = null
  private state: AiConnectionState = 'Disconnected'
  private readonly listeners = new Set<(state: AiConnectionState) => void>()

  private baseUrl: string
  private readonly fetchImpl: typeof fetch

  constructor(
    baseUrl: string = DEFAULT_AI_URL,
    fetchImpl: typeof fetch = (...args) => fetch(...args),
  ) {
    this.baseUrl = baseUrl
    this.fetchImpl = fetchImpl
  }

  getState(): AiConnectionState {
    return this.state
  }

  onStateChange(listener: (state: AiConnectionState) => void): () => void {
    this.listeners.add(listener)
    return () => this.listeners.delete(listener)
  }

  private setState(next: AiConnectionState): void {
    this.state = next
    for (const listener of this.listeners) listener(next)
  }

  setBaseUrl(url: string): void {
    this.assertLoopback(url)
    this.baseUrl = url.replace(/\/+$/, '')
    this.disconnect()
  }

  disconnect(): void {
    this.token = null
    this.setState('Disconnected')
  }

  private assertLoopback(url: string): void {
    if (!isLoopbackUrl(url)) {
      throw new AiApiError(0, 'not_loopback', 'The AI service URL must be a loopback address.')
    }
  }

  private url(path: string): string {
    return this.baseUrl.replace(/\/+$/, '') + path
  }

  private async failure(response: Response): Promise<AiApiError> {
    let code = 'http_' + response.status
    let message = response.statusText || 'Request failed'
    try {
      const body = (await response.json()) as { error?: { code?: string; message?: string } }
      code = body.error?.code ?? code
      message = body.error?.message ?? message
    } catch {
      // body was not JSON; keep the status-derived values
    }
    if (response.status === 401) this.disconnect()
    return new AiApiError(response.status, code, message)
  }

  async health(signal?: AbortSignal): Promise<AiHealth> {
    this.assertLoopback(this.baseUrl)
    const response = await this.fetchImpl(this.url('/health'), { signal })
    if (!response.ok) throw await this.failure(response)
    return (await response.json()) as AiHealth
  }

  async pair(code: string, signal?: AbortSignal): Promise<void> {
    this.assertLoopback(this.baseUrl)
    this.setState('Connecting')
    try {
      const response = await this.fetchImpl(this.url('/pair'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code: code.trim() }),
        signal,
      })
      if (!response.ok) throw await this.failure(response)
      const body = (await response.json()) as { token?: string }
      if (!body.token)
        throw new AiApiError(response.status, 'invalid_response', 'No token returned.')
      this.token = body.token
      this.setState('Paired')
    } catch (error) {
      this.token = null
      this.setState('Error')
      throw error
    }
  }

  /** Streams a grounded answer. Throws AiApiError for non-2xx replies; stream-level errors arrive as events. */
  ask(
    question: string,
    useKnowledge: boolean,
    signal?: AbortSignal,
  ): AsyncGenerator<AiStreamEvent> {
    return this.stream('/ask', { question, useKnowledge }, signal)
  }

  /** Streams a code review, debug analysis or test generation for the given source. */
  workflow(
    kind: AiWorkflow,
    request: AiCodeRequest,
    signal?: AbortSignal,
  ): AsyncGenerator<AiStreamEvent> {
    return this.stream('/' + kind, request, signal)
  }

  private async *stream(
    path: string,
    body: unknown,
    signal?: AbortSignal,
  ): AsyncGenerator<AiStreamEvent> {
    this.assertLoopback(this.baseUrl)
    if (!this.token) throw new AiApiError(401, 'not_paired', 'Pair with the AI service first.')
    const response = await this.fetchImpl(this.url(path), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${this.token}` },
      body: JSON.stringify(body),
      signal,
    })
    if (!response.ok) throw await this.failure(response)
    if (!response.body) throw new AiApiError(response.status, 'no_stream', 'The reply had no body.')
    yield* readSse(response.body)
  }
}
