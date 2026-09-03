// Re-exports monaco-editor's built-in editor worker entry so Vite/Rolldown can bundle it as a
// classic worker chunk with base-path-safe URL resolution (see MonacoEditor.tsx).
import '../../node_modules/monaco-editor/esm/vs/editor/editor.worker.js'
