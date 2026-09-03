import { useEffect, useImperativeHandle, useRef, forwardRef } from 'react'
// Import only the editor core plus the C# language contribution instead of `monaco-editor`
// (which re-exports every bundled language) to keep the production bundle small (§19.2).
// Relative node_modules paths are used because this monaco-editor version's package.json
// `exports` map only exposes `.` and TypeScript cannot resolve the deep subpaths otherwise.
import * as monaco from '../../node_modules/monaco-editor/esm/vs/editor/editor.api.js'
import '../../node_modules/monaco-editor/esm/vs/languages/definitions/csharp/csharp.js'
import type { EditorSettings } from '../editor/editorSettings'

// Vite base-path-safe Monaco worker resolution (FR/§17): only the editor's core worker is
// needed since C# uses Monaco's built-in Monarch tokenizer (no language server in Version 1).
// Uses the standard `new Worker(new URL(...), { type: 'module' })` pattern instead of the
// `?worker` import suffix so the URL is resolved and rewritten relative to the build's base
// path, keeping it valid both at the site root and under a GitHub Pages repository subpath.
self.MonacoEnvironment = {
  getWorker() {
    return new Worker(new URL('../workers/monacoEditorWorkerEntry.ts', import.meta.url), {
      type: 'module',
    })
  },
}

export interface MonacoEditorHandle {
  getValue: () => string
  setValue: (value: string) => void
  revealLine: (line: number) => void
  focus: () => void
}

export interface MonacoEditorProps {
  defaultValue: string
  onChange?: (value: string) => void
  theme?: 'vs-dark' | 'vs-light'
  markers?: monaco.editor.IMarkerData[]
  settings: EditorSettings
}

/**
 * Wraps the Monaco editor for a single in-memory `Program.cs` model (FR-001 through FR-010).
 * Avoids recreating the editor instance across React renders per §19.2.
 */
const MonacoEditor = forwardRef<MonacoEditorHandle, MonacoEditorProps>(function MonacoEditor(
  { defaultValue, onChange, theme = 'vs-dark', markers, settings },
  ref,
) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null)

  useEffect(() => {
    if (!containerRef.current) {
      return
    }

    const model = monaco.editor.createModel(defaultValue, 'csharp', monaco.Uri.file('Program.cs'))

    const editor = monaco.editor.create(containerRef.current, {
      model,
      theme,
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 14,
      scrollBeyondLastLine: false,
    })

    const subscription = editor.onDidChangeModelContent(() => {
      onChange?.(editor.getValue())
    })

    editorRef.current = editor

    return () => {
      subscription.dispose()
      editor.dispose()
      model.dispose()
      editorRef.current = null
    }
    // Intentionally run once: the model is the single source of truth for the editor's
    // lifetime, matching §19.2's guidance to avoid recreating Monaco during renders.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    monaco.editor.setTheme(theme)
  }, [theme])

  useEffect(() => {
    editorRef.current?.updateOptions({
      fontSize: settings.fontSize,
      fontFamily: settings.fontFamily,
      lineHeight: settings.lineHeight,
      tabSize: settings.tabSize,
      wordWrap: settings.wordWrap,
      minimap: { enabled: settings.minimap },
      renderWhitespace: settings.renderWhitespace,
      stickyScroll: { enabled: settings.stickyScroll },
      smoothScrolling: settings.smoothScrolling,
      cursorStyle: settings.cursorStyle,
      folding: settings.folding,
      autoIndent: settings.autoIndent ? 'full' : 'none',
    })
  }, [settings])

  useEffect(() => {
    const editor = editorRef.current
    const model = editor?.getModel()
    if (!model) {
      return
    }
    monaco.editor.setModelMarkers(model, 'codedotnet', markers ?? [])
  }, [markers])

  useImperativeHandle(ref, () => ({
    getValue: () => editorRef.current?.getValue() ?? '',
    setValue: (value: string) => editorRef.current?.setValue(value),
    revealLine: (line: number) => {
      editorRef.current?.revealLineInCenter(line)
      editorRef.current?.setPosition({ lineNumber: line, column: 1 })
    },
    focus: () => editorRef.current?.focus(),
  }))

  return <div ref={containerRef} className="monaco-editor-container" />
})

export default MonacoEditor
