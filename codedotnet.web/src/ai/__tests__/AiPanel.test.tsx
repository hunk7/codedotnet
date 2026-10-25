import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import AiPanel from '../AiPanel'

function sse(...blocks: string[]): Response {
  const encoder = new TextEncoder()
  return new Response(
    new ReadableStream({
      start(controller) {
        for (const block of blocks) controller.enqueue(encoder.encode(block))
        controller.close()
      },
    }),
    { status: 200, headers: { 'Content-Type': 'text/event-stream' } },
  )
}

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('AiPanel', () => {
  it('starts disconnected with asking disabled and makes no network call', () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    render(<AiPanel />)

    expect(screen.getByText('Disconnected')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Ask' }) as HTMLButtonElement).disabled).toBe(true)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('refuses a non-loopback URL without calling fetch', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    render(<AiPanel />)

    fireEvent.change(screen.getByLabelText('AI service URL'), {
      target: { value: 'http://example.com/v1' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Check service' }))

    await screen.findByText(/must be a loopback address/)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows a friendly message when the service is not running', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    render(<AiPanel />)

    fireEvent.click(screen.getByRole('button', { name: 'Check service' }))
    await screen.findByText(/Could not reach the AI service/)
  })

  it('pairs, streams an answer as plain text and lists citations', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/pair')) return json(200, { token: 't' })
      if (url.endsWith('/health'))
        return json(200, { status: 'ok', checks: [{ name: 'service', status: 'ok' }] })
      return sse(
        'event: token\ndata: {"text":"Span is <b>a view</b> [1]"}\n\n',
        'event: citation\ndata: {"label":1,"chunkId":"d-0","source":"span.md","title":"Span","heading":"Intro"}\n\n',
        'event: done\ndata: {"outcome":"ok","grounded":true}\n\n',
      )
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<AiPanel />)

    fireEvent.change(screen.getByLabelText('Pairing code'), { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: 'Pair' }))
    await screen.findByText('Paired')
    await screen.findByText(/Service: ok/)

    fireEvent.change(screen.getByLabelText('Question'), { target: { value: 'What is Span?' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ask' }))

    const output = await screen.findByText(/Span is/)
    expect(output.textContent).toBe('Span is <b>a view</b> [1]')
    expect(output.querySelector('b')).toBeNull()
    await waitFor(() => expect(screen.getByLabelText('Citations').textContent).toContain('span.md'))
  })

  it('reports insufficient evidence and stream errors', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input)
      if (url.endsWith('/pair')) return json(200, { token: 't' })
      if (url.endsWith('/health')) return json(200, { status: 'ok', checks: [] })
      return sse(
        'event: token\ndata: {"text":"x"}\n\n',
        'event: error\ndata: {"code":"provider_error","message":"model offline"}\n\n',
      )
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<AiPanel />)

    fireEvent.change(screen.getByLabelText('Pairing code'), { target: { value: '1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Pair' }))
    await screen.findByText('Paired')
    fireEvent.change(screen.getByLabelText('Question'), { target: { value: 'q' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ask' }))

    await screen.findByText('provider_error: model offline')
  })

  it('reviews the editor code from the shared context and shows evidence and the response', async () => {
    const bodies: { url: string; body: unknown }[] = []
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/pair')) return json(200, { token: 't' })
      if (url.endsWith('/health')) return json(200, { status: 'ok', checks: [] })
      bodies.push({ url, body: JSON.parse(String(init?.body)) })
      return sse(
        'event: evidence\ndata: {"compiles":true,"diagnostics":[],"findings":[{"line":1}]}\n\n',
        'event: token\ndata: {"text":"Looks fine"}\n\n',
        'event: done\ndata: {"outcome":"ok"}\n\n',
      )
    })
    vi.stubGlobal('fetch', fetchMock)
    render(
      <AiPanel
        context={{
          source: 'Console.WriteLine(1);',
          lastRun: {
            origin: 'editor',
            status: 'ExecutionCompleted',
            output: '1',
            diagnosticCount: 0,
          },
        }}
      />,
    )

    expect(screen.getByText(/last run \(editor\): ExecutionCompleted/)).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Pairing code'), { target: { value: '1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Pair' }))
    await screen.findByText('Paired')

    fireEvent.click(screen.getByRole('button', { name: 'Review code' }))
    await screen.findByText('Looks fine')
    expect(screen.getByText(/1 analyzer finding/)).toBeTruthy()
    expect(bodies).toHaveLength(1)
    expect(bodies[0].url.endsWith('/review')).toBe(true)
    expect(bodies[0].body).toMatchObject({ source: 'Console.WriteLine(1);' })
    expect((bodies[0].body as { focus: string }).focus).toContain('ExecutionCompleted')
  })

  it('disables code actions when the editor is empty', () => {
    vi.stubGlobal('fetch', vi.fn())
    render(<AiPanel />)
    expect(screen.getByText('The editor is empty.')).toBeTruthy()
    expect(
      (screen.getByRole('button', { name: 'Review code' }) as HTMLButtonElement).disabled,
    ).toBe(true)
  })

  it('includes the editor code in a free-form question', async () => {
    let question = ''
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/pair')) return json(200, { token: 't' })
      if (url.endsWith('/health')) return json(200, { status: 'ok', checks: [] })
      question = (JSON.parse(String(init?.body)) as { question: string }).question
      return sse('event: token\ndata: {"text":"ok"}\n\n', 'event: done\ndata: {}\n\n')
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<AiPanel context={{ source: 'int x = 1;', lastRun: null }} />)

    fireEvent.change(screen.getByLabelText('Pairing code'), { target: { value: '1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Pair' }))
    await screen.findByText('Paired')
    fireEvent.change(screen.getByLabelText('Question'), { target: { value: 'Why?' } })
    fireEvent.click(screen.getByRole('button', { name: 'Ask' }))

    await screen.findByText('ok')
    expect(question).toContain('Why?')
    expect(question).toContain('int x = 1;')
  })

  it('shows the pairing failure message', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        json(401, { error: { code: 'pairing_failed', message: 'Invalid pairing code.' } }),
      ),
    )
    render(<AiPanel />)

    fireEvent.change(screen.getByLabelText('Pairing code'), { target: { value: '000000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Pair' }))
    await screen.findByText('Invalid pairing code.')
    expect(screen.getByText('Error')).toBeTruthy()
  })
})
