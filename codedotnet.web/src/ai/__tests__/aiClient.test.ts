import { describe, expect, it } from 'vitest'
import {
  AiApiError,
  AiClient,
  isLoopbackUrl,
  parseSseBlock,
  readSse,
  type AiStreamEvent,
} from '../aiClient'

function streamOf(chunks: string[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder()
  return new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(encoder.encode(chunk))
      controller.close()
    },
  })
}

async function collect(iterable: AsyncIterable<AiStreamEvent>): Promise<AiStreamEvent[]> {
  const events: AiStreamEvent[] = []
  for await (const event of iterable) events.push(event)
  return events
}

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('isLoopbackUrl', () => {
  it.each([
    'http://127.0.0.1:7901/v1',
    'http://localhost:7901/v1',
    'http://[::1]:7901/v1',
    'https://127.0.0.1:7901/v1',
  ])('accepts %s', (url) => expect(isLoopbackUrl(url)).toBe(true))

  it.each([
    'http://192.168.1.5:7901/v1',
    'http://example.com/v1',
    'http://127.0.0.1.evil.com/v1',
    'ws://127.0.0.1:7901',
    'not a url',
    '',
  ])('rejects %s', (url) => expect(isLoopbackUrl(url)).toBe(false))
})

describe('parseSseBlock', () => {
  it('parses token, citation, done and error events', () => {
    expect(parseSseBlock('event: token\ndata: {"text":"hi"}')).toEqual({
      type: 'token',
      text: 'hi',
    })
    expect(
      parseSseBlock(
        'event: citation\ndata: {"label":1,"chunkId":"d-0","source":"s","title":"t","heading":"h"}',
      ),
    ).toMatchObject({ type: 'citation', citation: { label: 1, chunkId: 'd-0' } })
    expect(parseSseBlock('event: done\ndata: {"outcome":"ok"}')).toEqual({
      type: 'done',
      data: { outcome: 'ok' },
    })
    expect(parseSseBlock('event: error\ndata: {"code":"provider_error","message":"down"}')).toEqual(
      {
        type: 'error',
        code: 'provider_error',
        message: 'down',
      },
    )
  })

  it('returns null for malformed, unknown or incomplete blocks', () => {
    expect(parseSseBlock('')).toBeNull()
    expect(parseSseBlock('event: token')).toBeNull()
    expect(parseSseBlock('data: {"text":"x"}')).toBeNull()
    expect(parseSseBlock('event: token\ndata: not json')).toBeNull()
    expect(parseSseBlock('event: token\ndata: 5')).toBeNull()
    expect(parseSseBlock('event: token\ndata: {"text":5}')).toBeNull()
    expect(parseSseBlock('event: mystery\ndata: {}')).toBeNull()
  })

  it('tolerates CRLF line endings', () => {
    expect(parseSseBlock('event: token\r\ndata: {"text":"a"}\r')).toEqual({
      type: 'token',
      text: 'a',
    })
  })
})

describe('readSse', () => {
  it('reassembles events split across chunk boundaries', async () => {
    const events = await collect(
      readSse(
        streamOf([
          'event: token\ndata: {"te',
          'xt":"Hel"}\n\nevent: token\nda',
          'ta: {"text":"lo"}\n\nevent: done\ndata: {"outcome":"ok"}\n\n',
        ]),
      ),
    )
    expect(events.map((e) => e.type)).toEqual(['token', 'token', 'done'])
    expect(events[0]).toEqual({ type: 'token', text: 'Hel' })
  })

  it('handles CRLF separators and a final block without a trailing blank line', async () => {
    const events = await collect(
      readSse(
        streamOf([
          'event: token\r\ndata: {"text":"a"}\r\n\r\nevent: done\r\ndata: {"outcome":"ok"}',
        ]),
      ),
    )
    expect(events.map((e) => e.type)).toEqual(['token', 'done'])
  })

  it('skips unknown events without stopping the stream', async () => {
    const events = await collect(
      readSse(streamOf(['event: ping\ndata: {}\n\nevent: token\ndata: {"text":"x"}\n\n'])),
    )
    expect(events).toEqual([{ type: 'token', text: 'x' }])
  })
})

describe('AiClient', () => {
  it('refuses a non-loopback base URL before sending anything', async () => {
    const calls: string[] = []
    const client = new AiClient('http://example.com/v1', async (input) => {
      calls.push(String(input))
      return json(200, {})
    })
    await expect(client.health()).rejects.toMatchObject({ code: 'not_loopback' })
    await expect(client.pair('123456')).rejects.toMatchObject({ code: 'not_loopback' })
    expect(() => client.setBaseUrl('http://evil.test/v1')).toThrow(AiApiError)
    expect(calls).toEqual([])
  })

  it('pairs, then sends the bearer token on ask and streams the answer', async () => {
    const seen: { url: string; init?: RequestInit }[] = []
    const client = new AiClient('http://127.0.0.1:7901/v1', async (input, init) => {
      seen.push({ url: String(input), init })
      if (String(input).endsWith('/pair')) return json(200, { token: 'tok-1', expiresAt: 'x' })
      return new Response(
        streamOf(['event: token\ndata: {"text":"A"}\n\nevent: done\ndata: {"outcome":"ok"}\n\n']),
        { status: 200, headers: { 'Content-Type': 'text/event-stream' } },
      )
    })
    const states: string[] = []
    client.onStateChange((s) => states.push(s))

    await client.pair(' 123456 ')
    expect(JSON.parse(String(seen[0].init?.body))).toEqual({ code: '123456' })
    const events = await collect(client.ask('what is Span?', true))

    expect(events.map((e) => e.type)).toEqual(['token', 'done'])
    expect((seen[1].init?.headers as Record<string, string>).Authorization).toBe('Bearer tok-1')
    expect(JSON.parse(String(seen[1].init?.body))).toEqual({
      question: 'what is Span?',
      useKnowledge: true,
    })
    expect(states).toEqual(['Connecting', 'Paired'])
  })

  it('rejects ask before pairing', async () => {
    const client = new AiClient('http://127.0.0.1:7901/v1', async () => json(200, {}))
    await expect(collect(client.ask('q', true))).rejects.toMatchObject({
      status: 401,
      code: 'not_paired',
    })
  })

  it('reports a failed pairing and keeps no token', async () => {
    const client = new AiClient('http://127.0.0.1:7901/v1', async () =>
      json(401, { error: { code: 'pairing_failed', message: 'Invalid pairing code.' } }),
    )
    await expect(client.pair('000000')).rejects.toMatchObject({
      code: 'pairing_failed',
      status: 401,
    })
    expect(client.getState()).toBe('Error')
    await expect(collect(client.ask('q', true))).rejects.toMatchObject({ code: 'not_paired' })
  })

  it('drops the token when the service answers 401', async () => {
    let first = true
    const client = new AiClient('http://127.0.0.1:7901/v1', async (input) => {
      if (String(input).endsWith('/pair')) return json(200, { token: 't' })
      first = false
      return json(401, { error: { code: 'unauthorized', message: 'expired' } })
    })
    await client.pair('1')
    await expect(collect(client.ask('q', true))).rejects.toMatchObject({ status: 401 })
    expect(first).toBe(false)
    expect(client.getState()).toBe('Disconnected')
  })

  it('surfaces non-JSON error bodies with a status-derived code', async () => {
    const client = new AiClient(
      'http://127.0.0.1:7901/v1',
      async () => new Response('boom', { status: 500 }),
    )
    await expect(client.health()).rejects.toMatchObject({ status: 500, code: 'http_500' })
  })

  it('returns health checks', async () => {
    const client = new AiClient('http://127.0.0.1:7901/v1', async () =>
      json(200, {
        status: 'degraded',
        checks: [{ name: 'ollama', status: 'unavailable', detail: 'refused' }],
      }),
    )
    const health = await client.health()
    expect(health.status).toBe('degraded')
    expect(health.checks[0].name).toBe('ollama')
  })

  it('passes the abort signal to fetch', async () => {
    let received: AbortSignal | undefined
    const client = new AiClient('http://127.0.0.1:7901/v1', async (_input, init) => {
      received = init?.signal ?? undefined
      return json(200, { status: 'ok', checks: [] })
    })
    const controller = new AbortController()
    await client.health(controller.signal)
    expect(received).toBe(controller.signal)
  })

  it('disconnect clears the token', async () => {
    const client = new AiClient('http://127.0.0.1:7901/v1', async () => json(200, { token: 't' }))
    await client.pair('1')
    client.disconnect()
    expect(client.getState()).toBe('Disconnected')
    await expect(collect(client.ask('q', true))).rejects.toMatchObject({ code: 'not_paired' })
  })
})
