import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
// Trimmed import matching MonacoEditor.tsx to avoid pulling the full monaco-editor bundle
// (see that file's comment for why deep relative node_modules paths are used here).
import './App.css'
import { WorkerManager, type WorkerManagerState } from './workers/workerManager'
import type { CompileAndRunResponsePayload, DiagnosticPayload, RuntimeInformation } from './workers/protocol'
import MonacoEditor, { type MonacoEditorHandle } from './components/MonacoEditor'
import SettingsPanel from './components/SettingsPanel'
import AboutDialog from './components/AboutDialog'
import ErrorBoundary from './components/ErrorBoundary'
import { diagnosticsToMarkers } from './diagnostics'
import {
  clearAllLocalData,
  DEFAULT_EDITOR_SETTINGS,
  DEFAULT_LAYOUT_SETTINGS,
  DEFAULT_SOURCE,
  DEFAULT_THEME,
  loadEditorSettings,
  loadLayoutSettings,
  loadSource,
  loadStdin,
  loadTheme,
  resolveTheme,
  saveEditorSettings,
  saveLayoutSettings,
  saveSource,
  saveStdin,
  saveTheme,
  type EditorSettings,
  type LayoutSettings,
  type Theme,
} from './editor/editorSettings'
import { detectFeatureSupport, getMissingRequiredFeatures } from './browserSupport'

type BuildStatus =
  | 'Idle'
  | 'Compiling'
  | 'Running'
  | 'BuildFailed'
  | 'ExecutionCompleted'
  | 'ExecutionCompletedWithWarnings'
  | 'ExecutionFailed'
  | 'ExecutionStopped'
  | 'ExecutionTimedOut'

function App() {
  const managerRef = useRef<WorkerManager | null>(null)
  const editorRef = useRef<MonacoEditorHandle | null>(null)
  const workspaceRef = useRef<HTMLElement | null>(null)

  const [initStatus, setInitStatus] = useState('Loading development environment...')
  const [workerState, setWorkerState] = useState<WorkerManagerState>('Uninitialized')
  const [initError, setInitError] = useState<string | null>(null)

  const [theme, setTheme] = useState<Theme>(() => loadTheme())
  const [editorSettings, setEditorSettings] = useState<EditorSettings>(() => loadEditorSettings())
  const [layoutSettings, setLayoutSettings] = useState<LayoutSettings>(() => loadLayoutSettings())
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [focusMode, setFocusMode] = useState<'none' | 'editor' | 'io'>('none')
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [saveState, setSaveState] = useState<'saved' | 'pending' | 'unavailable'>('saved')

  const [source, setSource] = useState(() => loadSource())
  const [stdin, setStdin] = useState(() => loadStdin())

  const [buildStatus, setBuildStatus] = useState<BuildStatus>('Idle')
  const [diagnostics, setDiagnostics] = useState<DiagnosticPayload[]>([])
  const [output, setOutput] = useState('')
  const [outputTruncated, setOutputTruncated] = useState(false)
  const [exception, setException] = useState<{ type: string; message: string; stack: string | null } | null>(null)
  const [timings, setTimings] = useState<{ compile: number; execute: number; total: number } | null>(null)
  const [runtimeInfo, setRuntimeInfo] = useState<RuntimeInformation | null>(null)
  const [aboutOpen, setAboutOpen] = useState(false)

  const [missingFeatures] = useState<string[]>(() => getMissingRequiredFeatures(detectFeatureSupport()))

  useEffect(() => {
    if (missingFeatures.length > 0) {
      // Unsupported environment (NFR-C004): skip worker boot entirely and show the
      // compatibility fallback UI instead of an unexplained failure.
      return
    }

    const manager = new WorkerManager()
    managerRef.current = manager
    let cancelled = false

    const unsubscribe = manager.onStateChange((state) => {
      if (!cancelled) setWorkerState(state)
    })

    async function boot() {
      try {
        setInitStatus('Loading .NET runtime and C# compiler...')
        await manager.initialize()
        if (cancelled) return
        setInitStatus('Ready')
        try {
          const info = await manager.getRuntimeInformation()
          if (!cancelled) setRuntimeInfo(info)
        } catch {
          // Non-fatal: About dialog will show 'unavailable' if this fails.
        }
      } catch (err) {
        if (!cancelled) {
          setInitError((err as Error).message)
        }
      }
    }

    void boot()

    return () => {
      cancelled = true
      unsubscribe()
      manager.stop()
    }
  }, [])

  useEffect(() => {
    saveEditorSettings(editorSettings)
  }, [editorSettings])

  useEffect(() => {
    saveLayoutSettings(layoutSettings)
  }, [layoutSettings])

  useEffect(() => {
    saveTheme(theme)
  }, [theme])

  const resolvedTheme = (() => {
    // Recomputed on every render (cheap) rather than via useState+listener wiring, since
    // resolveTheme reads matchMedia synchronously and system-preference changes are rare.
    return resolveTheme(theme)
  })()

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', resolvedTheme)
  }, [resolvedTheme])

  useEffect(() => {
    if (theme !== 'system' || !window.matchMedia) return
    const media = window.matchMedia('(prefers-color-scheme: light)')
    const onChange = () => {
      document.documentElement.setAttribute('data-theme', resolveTheme('system'))
    }
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [theme])

  useEffect(() => {
    function onFullscreenChange() {
      setIsFullscreen(Boolean(document.fullscreenElement))
    }
    document.addEventListener('fullscreenchange', onFullscreenChange)
    return () => document.removeEventListener('fullscreenchange', onFullscreenChange)
  }, [])

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== 'Escape') return

      if (document.fullscreenElement) {
        void document.exitFullscreen()
      }
      setFocusMode('none')
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const toggleFullscreen = useCallback(() => {
    const container = workspaceRef.current?.closest('.app-shell') as HTMLElement | null
    if (!container) return

    if (document.fullscreenElement) {
      void document.exitFullscreen()
    } else if (container.requestFullscreen) {
      void container.requestFullscreen()
    } else {
      // Internal fallback for browsers without Fullscreen API support.
      setIsFullscreen((prev) => !prev)
    }
  }, [])

  const restoreDefaultLayout = useCallback(() => {
    setLayoutSettings({ ...DEFAULT_LAYOUT_SETTINGS })
    setFocusMode('none')
  }, [])

  const persistSource = useCallback((value: string) => {
    setSource(value)
    setSaveState('pending')
  }, [])

  const persistStdin = useCallback((value: string) => {
    setStdin(value)
    setSaveState('pending')
  }, [])

  // Debounced autosave for source/stdin (§16.3): avoids writing to localStorage on every
  // keystroke while still surfacing a save-state indicator to the user.
  useEffect(() => {
    if (saveState !== 'pending') return

    const handle = window.setTimeout(() => {
      const sourceSaved = saveSource(source)
      const stdinSaved = saveStdin(stdin)
      setSaveState(sourceSaved && stdinSaved ? 'saved' : 'unavailable')
    }, 500)

    return () => window.clearTimeout(handle)
  }, [source, stdin, saveState])

  const handleClearLocalData = useCallback(() => {
    clearAllLocalData()
    setSource(DEFAULT_SOURCE)
    setStdin('')
    setEditorSettings({ ...DEFAULT_EDITOR_SETTINGS })
    setLayoutSettings({ ...DEFAULT_LAYOUT_SETTINGS })
    setTheme(DEFAULT_THEME)
    setSaveState('saved')
  }, [])

  const isBusy = buildStatus === 'Compiling' || buildStatus === 'Running'
  const canRun = workerState === 'Ready' && !isBusy
  const canStop = isBusy

  const handleRun = useCallback(async () => {
    const manager = managerRef.current
    if (!manager) return

    const snapshot = editorRef.current?.getValue() ?? source
    setBuildStatus('Compiling')
    setDiagnostics([])
    setOutput('')
    setOutputTruncated(false)
    setException(null)
    setTimings(null)

    try {
      const result: CompileAndRunResponsePayload = await manager.compileAndRun(snapshot, stdin)

      setDiagnostics(result.diagnostics)
      setOutput(result.output)
      setOutputTruncated(result.outputTruncated)
      setTimings({
        compile: result.compilationDurationMs,
        execute: result.executionDurationMs,
        total: result.totalDurationMs,
      })

      if (result.exceptionType) {
        setException({
          type: result.exceptionType,
          message: result.exceptionMessage ?? '',
          stack: result.exceptionStackTrace,
        })
      }

      setBuildStatus(result.status)
    } catch (err) {
      const message = (err as Error).message
      if (message === 'Execution timed out') {
        setBuildStatus('ExecutionTimedOut')
      } else {
        setBuildStatus('ExecutionFailed')
        setException({ type: 'WorkerError', message, stack: null })
      }
    }
  }, [source, stdin])

  const handleStop = useCallback(() => {
    const manager = managerRef.current
    if (!manager) return
    manager.stop()
    setBuildStatus('ExecutionStopped')
    void manager.initialize()
  }, [])

  const handleClearOutput = useCallback(() => {
    setDiagnostics([])
    setOutput('')
    setOutputTruncated(false)
    setException(null)
    setTimings(null)
    setBuildStatus('Idle')
  }, [])

  const handleResetProgram = useCallback(() => {
    editorRef.current?.setValue(DEFAULT_SOURCE)
    persistSource(DEFAULT_SOURCE)
    persistStdin('')
    handleClearOutput()
  }, [persistSource, persistStdin, handleClearOutput])

  const handleDownload = useCallback(() => {
    const value = editorRef.current?.getValue() ?? source
    const blob = new Blob([value], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'Program.cs'
    a.click()
    URL.revokeObjectURL(url)
  }, [source])

  const togglePaneOrder = useCallback(() => {
    setLayoutSettings((prev) => ({
      ...prev,
      paneOrder: prev.paneOrder === 'editor-left' ? 'io-left' : 'editor-left',
    }))
  }, [])

  const handleDividerPointerDown = useCallback((event: ReactPointerEvent<HTMLDivElement>) => {
    event.preventDefault()
    const workspace = workspaceRef.current
    if (!workspace) return

    const onPointerMove = (moveEvent: PointerEvent) => {
      const rect = workspace.getBoundingClientRect()
      const rawRatio = (moveEvent.clientX - rect.left) / rect.width
      const clampedRatio = Math.min(0.8, Math.max(0.2, rawRatio))

      setLayoutSettings((prev) => {
        const ratio = prev.paneOrder === 'editor-left' ? clampedRatio : 1 - clampedRatio
        return { ...prev, splitRatio: ratio }
      })
    }

    const onPointerUp = () => {
      window.removeEventListener('pointermove', onPointerMove)
      window.removeEventListener('pointerup', onPointerUp)
    }

    window.addEventListener('pointermove', onPointerMove)
    window.addEventListener('pointerup', onPointerUp)
  }, [])

  const handleDiagnosticSelect = useCallback((diagnostic: DiagnosticPayload) => {
    editorRef.current?.revealLine(diagnostic.startLine)
    editorRef.current?.focus()
  }, [])

  // Global keyboard shortcuts (FR-112): Ctrl/Cmd+Enter (Run), Shift+F5 (Stop),
  // Ctrl/Cmd+S (force an immediate local save), and F11 (focus/fullscreen). Find/Replace and
  // Undo/Redo are handled natively by Monaco while the editor has focus, so they are not
  // duplicated here.
  useEffect(() => {
    function onGlobalKeyDown(event: KeyboardEvent) {
      const isRunShortcut = (event.ctrlKey || event.metaKey) && event.key === 'Enter'
      const isStopShortcut = event.shiftKey && event.key === 'F5'
      const isSaveShortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's'
      const isFullscreenShortcut = event.key === 'F11'

      if (isRunShortcut) {
        event.preventDefault()
        if (canRun) {
          void handleRun()
        }
        return
      }

      if (isStopShortcut) {
        event.preventDefault()
        if (canStop) {
          handleStop()
        }
        return
      }

      if (isSaveShortcut) {
        event.preventDefault()
        const sourceSaved = saveSource(editorRef.current?.getValue() ?? source)
        const stdinSaved = saveStdin(stdin)
        setSaveState(sourceSaved && stdinSaved ? 'saved' : 'unavailable')
        return
      }

      if (isFullscreenShortcut) {
        event.preventDefault()
        toggleFullscreen()
      }
    }

    window.addEventListener('keydown', onGlobalKeyDown)
    return () => window.removeEventListener('keydown', onGlobalKeyDown)
  }, [canRun, canStop, handleRun, handleStop, toggleFullscreen, source, stdin])

  if (missingFeatures.length > 0) {
    return (
      <div className="app-shell app-shell--error">
        <h1>codedotnet is not supported in this browser</h1>
        <p>Your browser is missing required capabilities:</p>
        <ul>
          {missingFeatures.map((feature) => (
            <li key={feature}>{feature}</li>
          ))}
        </ul>
        <p>
          Please try the current stable release of Microsoft Edge, Google Chrome, Mozilla Firefox, or Apple
          Safari.
        </p>
      </div>
    )
  }

  if (initError) {
    return (
      <div className="app-shell app-shell--error">
        <p>Failed to initialize: {initError}</p>
        <button type="button" onClick={() => window.location.reload()}>
          Reload
        </button>
      </div>
    )
  }

  if (workerState !== 'Ready' && workerState !== 'Busy') {
    return (
      <div className="app-shell app-shell--loading">
        <p>{initStatus}</p>
      </div>
    )
  }

  const markers = diagnosticsToMarkers(diagnostics)
  const editorPane = (
    <ErrorBoundary label="Editor">
      <section className="pane pane--editor">
        <MonacoEditor
          ref={editorRef}
          defaultValue={source}
          onChange={persistSource}
          theme={resolvedTheme}
          markers={markers}
          settings={editorSettings}
        />
      </section>
    </ErrorBoundary>
  )

  const ioPane = (
    <ErrorBoundary label="I/O pane">
      <section className="pane pane--io">
      <div className="io-pane__stdin">
        <label htmlFor="stdin-input">STDIN</label>
        <textarea
          id="stdin-input"
          value={stdin}
          onChange={(e) => persistStdin(e.target.value)}
          placeholder="Optional input provided to the program before it runs"
        />
      </div>

      <div className="io-pane__output">
        <div className="io-pane__output-header">
          <span>Output</span>
          {timings && (
            <span className="io-pane__timings">
              build {timings.compile.toFixed(0)}ms · run {timings.execute.toFixed(0)}ms · total{' '}
              {timings.total.toFixed(0)}ms
            </span>
          )}
        </div>
        <pre className="io-pane__output-content">{output || '\u00A0'}</pre>
        {outputTruncated && (
          <p className="io-pane__truncation-notice">Output truncated because the maximum output limit was reached.</p>
        )}
        {exception && (
          <div className="io-pane__exception">
            <strong>
              {exception.type}: {exception.message}
            </strong>
            {exception.stack && <pre>{exception.stack}</pre>}
          </div>
        )}
      </div>

      <div className="io-pane__problems">
        <div className="io-pane__problems-header">Problems ({diagnostics.length})</div>
        <ul>
          {diagnostics.map((d, i) => (
            <li
              key={`${d.id}-${i}`}
              className={`problem problem--${d.severity.toLowerCase()}`}
              onClick={() => handleDiagnosticSelect(d)}
            >
              Program.cs({d.startLine},{d.startColumn}): {d.severity.toLowerCase()} {d.id}: {d.message}
            </li>
          ))}
        </ul>
      </div>
      </section>
    </ErrorBoundary>
  )

  return (
    <div className={`app-shell${isFullscreen ? ' app-shell--fullscreen' : ''}`}>
      <header className="app-shell__toolbar">
        <span className="app-shell__logo">codedotnet</span>

        <div className="app-shell__actions">
          <button
            type="button"
            onClick={() => void handleRun()}
            disabled={!canRun}
            title="Run (Ctrl/Cmd+Enter)"
          >
            {isBusy ? 'Running…' : 'Run'}
          </button>
          <button type="button" onClick={handleStop} disabled={!canStop} title="Stop (Shift+F5)">
            Stop
          </button>
          <button type="button" onClick={handleClearOutput}>
            Clear
          </button>
          <button type="button" onClick={handleResetProgram}>
            Reset
          </button>
          <button type="button" onClick={togglePaneOrder}>
            Swap panes
          </button>
          <button
            type="button"
            onClick={() => setFocusMode((prev) => (prev === 'editor' ? 'none' : 'editor'))}
          >
            {focusMode === 'editor' ? 'Exit focus' : 'Focus editor'}
          </button>
          <button type="button" onClick={() => setFocusMode((prev) => (prev === 'io' ? 'none' : 'io'))}>
            {focusMode === 'io' ? 'Exit focus' : 'Focus I/O'}
          </button>
          <button type="button" onClick={toggleFullscreen} title="Toggle fullscreen (F11)">
            {isFullscreen ? 'Exit fullscreen' : 'Fullscreen'}
          </button>
          <button type="button" onClick={restoreDefaultLayout}>
            Restore layout
          </button>
          <button type="button" onClick={handleDownload}>
            Download
          </button>
          <button type="button" onClick={() => setSettingsOpen(true)}>
            Settings
          </button>
          <button type="button" onClick={() => setAboutOpen(true)}>
            About
          </button>
        </div>
      </header>

      <main
        ref={(el) => {
          workspaceRef.current = el
        }}
        className="app-shell__workspace"
        data-pane-order={layoutSettings.paneOrder}
      >
        {focusMode === 'editor' ? (
          <div className="pane-wrapper" style={{ flexBasis: '100%' }}>
            {editorPane}
          </div>
        ) : focusMode === 'io' ? (
          <div className="pane-wrapper" style={{ flexBasis: '100%' }}>
            {ioPane}
          </div>
        ) : layoutSettings.paneOrder === 'editor-left' ? (
          <>
            <div className="pane-wrapper" style={{ flexBasis: `${layoutSettings.splitRatio * 100}%` }}>
              {editorPane}
            </div>
            <div
              className="pane-divider"
              onPointerDown={handleDividerPointerDown}
              role="separator"
              aria-orientation="vertical"
            />
            <div className="pane-wrapper" style={{ flexBasis: `${(1 - layoutSettings.splitRatio) * 100}%` }}>
              {ioPane}
            </div>
          </>
        ) : (
          <>
            <div className="pane-wrapper" style={{ flexBasis: `${layoutSettings.splitRatio * 100}%` }}>
              {ioPane}
            </div>
            <div
              className="pane-divider"
              onPointerDown={handleDividerPointerDown}
              role="separator"
              aria-orientation="vertical"
            />
            <div className="pane-wrapper" style={{ flexBasis: `${(1 - layoutSettings.splitRatio) * 100}%` }}>
              {editorPane}
            </div>
          </>
        )}
      </main>

      <footer className="app-shell__status-bar">
        <span>{buildStatus}</span>
        <span>{workerState}</span>
        <span className={`save-state save-state--${saveState}`}>
          {saveState === 'saved' ? 'Saved' : saveState === 'pending' ? 'Saving…' : 'Storage unavailable'}
        </span>
        <button type="button" className="clear-data-button" onClick={handleClearLocalData}>
          Clear local data
        </button>
      </footer>

      <ErrorBoundary label="Settings">
        <SettingsPanel
          open={settingsOpen}
          settings={editorSettings}
          theme={theme}
          onChange={setEditorSettings}
          onThemeChange={setTheme}
          onClose={() => setSettingsOpen(false)}
        />
      </ErrorBoundary>

      <AboutDialog open={aboutOpen} runtimeInfo={runtimeInfo} onClose={() => setAboutOpen(false)} />
    </div>
  )
}

export default App
