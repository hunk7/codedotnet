import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import './aiPanel.css'
import {
  AiApiError,
  AiClient,
  DEFAULT_AI_URL,
  isLoopbackUrl,
  type AiCitation,
  type AiConnectionState,
  type AiHealth,
  type AiStreamEvent,
  type AiWorkflow,
} from './aiClient'
import {
  MAX_QUESTION_CHARS,
  buildContextQuestion,
  buildRunFocus,
  type AiWorkspaceContext,
} from './workspaceContext'

type AskState = 'Idle' | 'Streaming' | 'Done' | 'Error'

interface DoneInfo {
  stopReason?: string
  grounded?: boolean
  note?: string
  verified?: boolean
}

interface AiPanelProps {
  /** Current editor source and last run, supplied by the app so the AI tab works on the same program. */
  context?: AiWorkspaceContext
  /** Switches back to the editor tab. */
  onOpenEditor?: () => void
}

const EMPTY_CONTEXT: AiWorkspaceContext = { source: '', lastRun: null }

const WORKFLOW_LABEL: Record<AiWorkflow, string> = {
  review: 'Review',
  debug: 'Debug',
  tests: 'Tests',
}

function describeEvidence(data: Record<string, unknown>): string {
  const diagnostics = Array.isArray(data.diagnostics) ? data.diagnostics.length : 0
  const findings = Array.isArray(data.findings) ? data.findings.length : 0
  const parts = [
    data.compiles === false ? 'Does not compile' : 'Compiles',
    `${diagnostics} diagnostic(s)`,
    `${findings} analyzer finding(s)`,
  ]
  const run = data.run as { status?: string } | null | undefined
  if (run?.status) parts.push(`sandbox run: ${run.status}`)
  if (typeof data.runNote === 'string') parts.push(data.runNote)
  return parts.join(' · ')
}

/**
 * Optional local-AI view. The playground works without it: nothing here runs until the user pairs with a
 * CodeDotNet.Ai service on loopback. It works on the editor's current code and last run (passed in as
 * `context`), and renders answers as plain text (never HTML) with the knowledge-base citations they used.
 */
function AiPanel({ context = EMPTY_CONTEXT, onOpenEditor }: AiPanelProps) {
  const clientRef = useRef<AiClient | null>(null)
  const abortRef = useRef<AbortController | null>(null)

  const [url, setUrl] = useState(DEFAULT_AI_URL)
  const [code, setCode] = useState('')
  const [connection, setConnection] = useState<AiConnectionState>('Disconnected')
  const [health, setHealth] = useState<AiHealth | null>(null)
  const [connectError, setConnectError] = useState<string | null>(null)

  const [question, setQuestion] = useState('')
  const [useKnowledge, setUseKnowledge] = useState(true)
  const [includeCode, setIncludeCode] = useState(true)
  const [askState, setAskState] = useState<AskState>('Idle')
  const [mode, setMode] = useState('Answer')
  const [answer, setAnswer] = useState('')
  const [evidence, setEvidence] = useState<string | null>(null)
  const [progress, setProgress] = useState<string[]>([])
  const [citations, setCitations] = useState<AiCitation[]>([])
  const [done, setDone] = useState<DoneInfo | null>(null)
  const [askError, setAskError] = useState<string | null>(null)

  const getClient = useCallback((): AiClient => {
    if (!clientRef.current) {
      clientRef.current = new AiClient(DEFAULT_AI_URL)
      clientRef.current.onStateChange(setConnection)
    }
    return clientRef.current
  }, [])

  useEffect(
    () => () => {
      abortRef.current?.abort()
      clientRef.current?.disconnect()
    },
    [],
  )

  const handleCheckHealth = useCallback(async () => {
    setConnectError(null)
    if (!isLoopbackUrl(url)) {
      setConnectError('The AI service URL must be a loopback address (127.0.0.1 or localhost).')
      return
    }
    try {
      const client = getClient()
      client.setBaseUrl(url)
      setHealth(await client.health())
    } catch (err) {
      setHealth(null)
      setConnectError(
        err instanceof AiApiError
          ? err.message
          : 'Could not reach the AI service. Start it with scripts/dev/Start-CodeDotNet.ps1.',
      )
    }
  }, [getClient, url])

  const handlePair = useCallback(async () => {
    setConnectError(null)
    if (!isLoopbackUrl(url)) {
      setConnectError('The AI service URL must be a loopback address (127.0.0.1 or localhost).')
      return
    }
    try {
      const client = getClient()
      client.setBaseUrl(url)
      await client.pair(code)
      setCode('')
      setHealth(await client.health())
    } catch (err) {
      if (err instanceof AiApiError && err.code === 'pairing_code_expired') setCode('')
      setConnectError(
        err instanceof AiApiError
          ? err.code === 'pairing_code_expired'
            ? `${err.message} Copy the newest "Pairing code" line from the service console.`
            : err.message
          : 'Could not reach the AI service. Start it with scripts/dev/Start-CodeDotNet.ps1.',
      )
    }
  }, [code, getClient, url])

  const handleDisconnect = useCallback(() => {
    abortRef.current?.abort()
    getClient().disconnect()
    setHealth(null)
  }, [getClient])

  const consume = useCallback(
    async (label: string, events: (signal: AbortSignal) => AsyncGenerator<AiStreamEvent>) => {
      const controller = new AbortController()
      abortRef.current = controller
      setMode(label)
      setAnswer('')
      setEvidence(null)
      setProgress([])
      setCitations([])
      setDone(null)
      setAskError(null)
      setAskState('Streaming')
      let failed = false
      try {
        for await (const event of events(controller.signal)) {
          if (event.type === 'token') setAnswer((prev) => prev + event.text)
          else if (event.type === 'citation') setCitations((prev) => [...prev, event.citation])
          else if (event.type === 'evidence') setEvidence(describeEvidence(event.data))
          else if (event.type === 'attempt') {
            const n = String(event.data.attempt ?? '?')
            const failure = typeof event.data.failure === 'string' ? ` — ${event.data.failure}` : ''
            setProgress((prev) => [
              ...prev,
              `Attempt ${n}: ${event.data.passed === true ? 'passed' : 'did not pass'}${failure}`,
            ])
          } else if (event.type === 'result') setAnswer(event.code)
          else if (event.type === 'done') setDone(event.data as DoneInfo)
          else {
            failed = true
            setAskError(`${event.code}: ${event.message}`)
          }
        }
        setAskState(failed ? 'Error' : 'Done')
      } catch (err) {
        if (controller.signal.aborted) {
          setAskState('Idle')
          return
        }
        setAskError(err instanceof Error ? err.message : 'Request failed.')
        setAskState('Error')
      } finally {
        abortRef.current = null
      }
    },
    [],
  )

  const hasCode = context.source.trim().length > 0

  const handleAsk = useCallback(async () => {
    const text = question.trim()
    if (!text) return
    const full = includeCode && hasCode ? buildContextQuestion(text, context) : text
    await consume('Answer', (signal) => getClient().ask(full, useKnowledge, signal))
  }, [consume, context, getClient, hasCode, includeCode, question, useKnowledge])

  const handleWorkflow = useCallback(
    async (kind: AiWorkflow) => {
      if (!hasCode) return
      const focus = buildRunFocus(context.lastRun)
      await consume(WORKFLOW_LABEL[kind], (signal) =>
        getClient().workflow(kind, { source: context.source, focus }, signal),
      )
    },
    [consume, context, getClient, hasCode],
  )

  const handleStop = useCallback(() => abortRef.current?.abort(), [])

  const isPaired = connection === 'Paired'
  const isStreaming = askState === 'Streaming'
  const lineCount = useMemo(
    () => (hasCode ? context.source.split('\n').length : 0),
    [context.source, hasCode],
  )
  const run = context.lastRun
  const hasResult = answer || citations.length > 0 || done || evidence || progress.length > 0

  return (
    <div className="ai-panel" data-testid="ai-panel">
      <section className="mcp-playground__connection">
        <label className="mcp-playground__url-label" htmlFor="ai-url">
          AI service URL
        </label>
        <input
          id="ai-url"
          className="mcp-playground__url-input"
          value={url}
          onChange={(event) => setUrl(event.target.value)}
          disabled={isPaired}
          spellCheck={false}
        />
        {isPaired ? (
          <button
            type="button"
            className="toolbar-button toolbar-button--stop"
            onClick={handleDisconnect}
          >
            Disconnect
          </button>
        ) : (
          <>
            <label className="mcp-playground__url-label" htmlFor="ai-code">
              Pairing code
            </label>
            <input
              id="ai-code"
              className="mcp-playground__url-input ai-panel__code-input"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
            />
            <button
              type="button"
              className="toolbar-button toolbar-button--run"
              onClick={() => void handlePair()}
              disabled={connection === 'Connecting' || !code.trim()}
            >
              {connection === 'Connecting' ? 'Pairing…' : 'Pair'}
            </button>
            <button
              type="button"
              className="toolbar-button"
              onClick={() => void handleCheckHealth()}
            >
              Check service
            </button>
          </>
        )}
        <span
          className={`mcp-playground__status mcp-playground__status--${connection.toLowerCase()}`}
        >
          {connection}
        </span>
      </section>

      {connectError && <p className="mcp-playground__error">{connectError}</p>}

      {health && (
        <details
          className="mcp-playground__tools ai-panel__health"
          aria-label="Service health"
          open={health.status !== 'ok'}
        >
          <summary>Service: {health.status}</summary>
          <ul>
            {health.checks.map((check) => (
              <li key={check.name}>
                <code>{check.name}</code> — {check.status}
                {check.detail ? ` (${check.detail})` : ''}
              </li>
            ))}
          </ul>
        </details>
      )}

      <section className="ai-panel__context" aria-label="Current workspace">
        <div className="ai-panel__context-info">
          <strong>Working on</strong>
          {hasCode ? (
            <span>
              Editor code ({lineCount} line{lineCount === 1 ? '' : 's'})
              {run
                ? ` · last run (${run.origin === 'mcp' ? 'MCP' : 'editor'}): ${run.status}`
                : ' · not run yet'}
            </span>
          ) : (
            <span>The editor is empty.</span>
          )}
          {onOpenEditor && (
            <button type="button" className="toolbar-button" onClick={onOpenEditor}>
              Open editor
            </button>
          )}
        </div>
        <div className="ai-panel__context-actions">
          {(['review', 'debug', 'tests'] as const).map((kind) => (
            <button
              key={kind}
              type="button"
              className="toolbar-button"
              onClick={() => void handleWorkflow(kind)}
              disabled={!isPaired || isStreaming || !hasCode}
              title={
                kind === 'review'
                  ? 'Review the editor code using compiler and analyzer evidence'
                  : kind === 'debug'
                    ? 'Find the likely cause of the failure using the code and its run output'
                    : 'Generate tests for the editor code and verify them in the sandbox'
              }
            >
              {kind === 'review'
                ? 'Review code'
                : kind === 'debug'
                  ? 'Debug code'
                  : 'Generate tests'}
            </button>
          ))}
        </div>
      </section>

      <div className="ai-panel__grid">
        <section className="ai-panel__composer">
          <label className="mcp-playground__url-label" htmlFor="ai-question">
            Question
          </label>
          <textarea
            id="ai-question"
            className="mcp-playground__url-input ai-panel__question"
            rows={4}
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            disabled={!isPaired || isStreaming}
            maxLength={MAX_QUESTION_CHARS}
          />
          <label className="ai-panel__option">
            <input
              type="checkbox"
              checked={includeCode}
              onChange={(event) => setIncludeCode(event.target.checked)}
              disabled={isStreaming || !hasCode}
            />{' '}
            Include my editor code and last run
          </label>
          <label className="ai-panel__option">
            <input
              type="checkbox"
              checked={useKnowledge}
              onChange={(event) => setUseKnowledge(event.target.checked)}
              disabled={isStreaming}
            />{' '}
            Answer from my knowledge base (with citations)
          </label>
          <div className="mcp-playground__actions">
            {isStreaming ? (
              <button
                type="button"
                className="toolbar-button toolbar-button--stop"
                onClick={handleStop}
              >
                Stop
              </button>
            ) : (
              <button
                type="button"
                className="toolbar-button toolbar-button--run"
                onClick={() => void handleAsk()}
                disabled={!isPaired || !question.trim()}
              >
                Ask
              </button>
            )}
          </div>
        </section>

        <section className="ai-panel__result" aria-live="polite" aria-label="Response">
          {askError && <p className="mcp-playground__error">{askError}</p>}
          {isStreaming && !hasResult && <p className="mcp-playground__timings">Thinking…</p>}
          {!hasResult && !isStreaming && !askError && (
            <p className="mcp-playground__timings">
              {isPaired
                ? 'Ask a question, or review, debug or generate tests for your editor code. The response appears here.'
                : 'Pair with the local AI service to get started.'}
            </p>
          )}
          {hasResult && (
            <>
              <div className="ai-panel__result-heading">
                {mode}
                {isStreaming ? ' · streaming…' : ''}
              </div>
              {evidence && <p className="mcp-playground__timings">{evidence}</p>}
              {progress.length > 0 && (
                <ul className="mcp-playground__diagnostics">
                  {progress.map((line, i) => (
                    <li key={i}>{line}</li>
                  ))}
                </ul>
              )}
              <pre className="mcp-playground__output ai-panel__answer">
                {answer || (isStreaming ? '' : '(no answer text)')}
              </pre>
              {done && done.grounded === false && done.stopReason === 'insufficient_evidence' && (
                <p className="mcp-playground__timings">
                  Not enough evidence in the knowledge base to answer.
                </p>
              )}
              {done?.verified === true && (
                <p className="mcp-playground__timings">Verified in the sandbox.</p>
              )}
              {done?.verified === false && mode === 'Tests' && (
                <p className="mcp-playground__timings">
                  Not verified — treat these tests as a draft.
                </p>
              )}
              {done?.note && <p className="mcp-playground__timings">{done.note}</p>}
              {citations.length > 0 && (
                <ul className="mcp-playground__diagnostics" aria-label="Citations">
                  {citations.map((c) => (
                    <li key={`${c.label}-${c.chunkId}`}>
                      [{c.label}] {c.title}
                      {c.heading ? ` › ${c.heading}` : ''} — {c.source}
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
        </section>
      </div>
    </div>
  )
}

export default AiPanel
