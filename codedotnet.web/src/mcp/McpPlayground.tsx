import { useCallback, useRef, useState } from 'react'
import MonacoEditor, { type MonacoEditorHandle } from '../components/MonacoEditor'
import { DEFAULT_EDITOR_SETTINGS } from '../editor/editorSettings'
import { McpClient, type McpConnectionState, type McpTool } from './mcpClient'
import type { CompileAndRunResponsePayload } from '../workers/protocol'
import { PlayIcon } from '../components/icons'

const DEFAULT_MCP_URL = 'ws://127.0.0.1:7900'

const SAMPLE_SOURCE = `using System;

Console.WriteLine("Hello from the codedotnet MCP sandbox!");
`

type CallState = 'Idle' | 'Calling' | 'Done' | 'Error'

/**
 * A minimal, self-contained GUI for connecting to a running codedotnet-sandbox MCP
 * server over WebSocket (see codedotnet-sandbox/src/wsServer.mjs), listing its tools,
 * and invoking compile_and_run with live source editing - so the MCP capability can be
 * validated, edited against, and demonstrated directly from the browser instead of only
 * via a stdio client.
 */
function McpPlayground() {
  const clientRef = useRef<McpClient | null>(null)
  const editorRef = useRef<MonacoEditorHandle | null>(null)

  const [url, setUrl] = useState(DEFAULT_MCP_URL)
  const [connectionState, setConnectionState] = useState<McpConnectionState>('Disconnected')
  const [connectError, setConnectError] = useState<string | null>(null)
  const [tools, setTools] = useState<McpTool[]>([])

  const [source, setSource] = useState(SAMPLE_SOURCE)
  const [callState, setCallState] = useState<CallState>('Idle')
  const [result, setResult] = useState<CompileAndRunResponsePayload | null>(null)
  const [isToolError, setIsToolError] = useState(false)
  const [callError, setCallError] = useState<string | null>(null)
  const [rawResponse, setRawResponse] = useState<string>('')

  const getClient = useCallback((): McpClient => {
    if (!clientRef.current) {
      clientRef.current = new McpClient()
      clientRef.current.onStateChange(setConnectionState)
    }
    return clientRef.current
  }, [])

  const handleConnect = useCallback(async () => {
    const client = getClient()
    setConnectError(null)
    setTools([])
    try {
      await client.connect(url)
      await client.initialize()
      const discovered = await client.listTools()
      setTools(discovered)
    } catch (err) {
      setConnectError((err as Error).message)
    }
  }, [getClient, url])

  const handleDisconnect = useCallback(() => {
    clientRef.current?.disconnect()
    setTools([])
  }, [])

  const handleRun = useCallback(async () => {
    const client = clientRef.current
    if (!client || connectionState !== 'Connected') return

    const snapshot = editorRef.current?.getValue() ?? source
    setCallState('Calling')
    setResult(null)
    setCallError(null)
    setIsToolError(false)
    setRawResponse('')

    try {
      const callResult = await client.callTool('compile_and_run', {
        source: snapshot,
        timeoutMs: 10000,
      })
      const text = callResult.content[0]?.text ?? '{}'
      setRawResponse(text)
      setResult(JSON.parse(text) as CompileAndRunResponsePayload)
      setIsToolError(callResult.isError)
      setCallState('Done')
    } catch (err) {
      setCallError((err as Error).message)
      setCallState('Error')
    }
  }, [connectionState, source])

  const isConnected = connectionState === 'Connected'
  const isConnecting = connectionState === 'Connecting'
  const isCalling = callState === 'Calling'

  return (
    <div className="mcp-playground">
      <section className="mcp-playground__connection">
        <label className="mcp-playground__url-label" htmlFor="mcp-url">
          MCP server URL
        </label>
        <input
          id="mcp-url"
          className="mcp-playground__url-input"
          value={url}
          onChange={(event) => setUrl(event.target.value)}
          disabled={isConnected || isConnecting}
          spellCheck={false}
        />
        {isConnected ? (
          <button
            type="button"
            className="toolbar-button toolbar-button--stop"
            onClick={handleDisconnect}
          >
            Disconnect
          </button>
        ) : (
          <button
            type="button"
            className="toolbar-button toolbar-button--run"
            onClick={() => void handleConnect()}
            disabled={isConnecting}
          >
            {isConnecting ? 'Connecting…' : 'Connect'}
          </button>
        )}
        <span
          className={`mcp-playground__status mcp-playground__status--${connectionState.toLowerCase()}`}
        >
          {connectionState}
        </span>
      </section>

      {connectError && <p className="mcp-playground__error">{connectError}</p>}

      {tools.length > 0 && (
        <section className="mcp-playground__tools">
          <h2>Discovered tools</h2>
          <ul>
            {tools.map((tool) => (
              <li key={tool.name}>
                <code>{tool.name}</code> — {tool.description}
              </li>
            ))}
          </ul>
        </section>
      )}

      <section className="mcp-playground__workspace">
        <section className="mcp-playground__editor pane pane--editor">
          <MonacoEditor
            ref={editorRef}
            defaultValue={source}
            onChange={setSource}
            theme="vs-dark"
            settings={DEFAULT_EDITOR_SETTINGS}
          />
        </section>

        <div className="mcp-playground__actions">
          <button
            type="button"
            className="toolbar-button toolbar-button--run"
            onClick={() => void handleRun()}
            disabled={!isConnected || isCalling}
          >
            <PlayIcon />
            <span>{isCalling ? 'Calling tools/call…' : 'Call compile_and_run'}</span>
          </button>
        </div>
      </section>

      {callError && <p className="mcp-playground__error">{callError}</p>}

      {result && (
        <section className="mcp-playground__result">
          <div
            className={`mcp-playground__result-status${isToolError ? ' mcp-playground__result-status--error' : ''}`}
          >
            {result.status}
          </div>
          <pre className="mcp-playground__output">{result.output || '(no output)'}</pre>
          {result.diagnostics.length > 0 && (
            <ul className="mcp-playground__diagnostics">
              {result.diagnostics.map((d) => (
                <li key={d.id}>
                  [{d.severity}] {d.message}
                </li>
              ))}
            </ul>
          )}
          {result.exceptionMessage && (
            <p className="mcp-playground__exception">
              {result.exceptionType}: {result.exceptionMessage}
            </p>
          )}
          <p className="mcp-playground__timings">
            build {result.compilationDurationMs.toFixed(0)}ms · run{' '}
            {result.executionDurationMs.toFixed(0)}ms · total {result.totalDurationMs.toFixed(0)}ms
          </p>
          <details className="mcp-playground__raw">
            <summary>Raw JSON-RPC tools/call response</summary>
            <pre>{rawResponse}</pre>
          </details>
        </section>
      )}
    </div>
  )
}

export default McpPlayground
