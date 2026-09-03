export interface EditorSettings {
  fontSize: number
  fontFamily: string
  lineHeight: number
  tabSize: number
  wordWrap: 'on' | 'off'
  minimap: boolean
  renderWhitespace: 'none' | 'boundary' | 'all'
  stickyScroll: boolean
  smoothScrolling: boolean
  cursorStyle: 'line' | 'block' | 'underline'
  folding: boolean
  autoIndent: boolean
}

export interface LayoutSettings {
  paneOrder: 'editor-left' | 'io-left'
  splitRatio: number
}

export type Theme = 'system' | 'vs-dark' | 'vs-light'

export const DEFAULT_EDITOR_SETTINGS: EditorSettings = {
  fontSize: 14,
  fontFamily: "'Cascadia Code', 'Fira Code', Consolas, monospace",
  lineHeight: 20,
  tabSize: 4,
  wordWrap: 'off',
  minimap: false,
  renderWhitespace: 'none',
  stickyScroll: true,
  smoothScrolling: true,
  cursorStyle: 'line',
  folding: true,
  autoIndent: true,
}

export const DEFAULT_LAYOUT_SETTINGS: LayoutSettings = {
  paneOrder: 'editor-left',
  splitRatio: 0.6,
}

export const DEFAULT_THEME: Theme = 'system'

const STORAGE_VERSION = 1

const KEYS = {
  source: 'codedotnet.source',
  stdin: 'codedotnet.stdin',
  editorSettings: 'codedotnet.editor.settings',
  layoutSettings: 'codedotnet.layout.settings',
  theme: 'codedotnet.theme',
  storageVersion: 'codedotnet.storage.version',
} as const

const FONT_SIZE_RANGE = { min: 10, max: 24 }
const LINE_HEIGHT_RANGE = { min: 14, max: 36 }
const TAB_SIZE_RANGE = { min: 2, max: 8 }

function isStorageAvailable(): boolean {
  try {
    const key = '__codedotnet_probe__'
    window.localStorage.setItem(key, '1')
    window.localStorage.removeItem(key)
    return true
  } catch {
    return false
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

function readJson<T>(key: string): T | undefined {
  if (!isStorageAvailable()) {
    return undefined
  }
  const raw = window.localStorage.getItem(key)
  if (!raw) {
    return undefined
  }
  try {
    return JSON.parse(raw) as T
  } catch {
    return undefined
  }
}

function writeJson(key: string, value: unknown): boolean {
  if (!isStorageAvailable()) {
    return false
  }
  try {
    window.localStorage.setItem(key, JSON.stringify(value))
    return true
  } catch {
    // Storage quota exceeded or otherwise unavailable (FR-030).
    return false
  }
}

/** Validates and merges persisted editor settings with defaults (§16.3). */
export function loadEditorSettings(): EditorSettings {
  const stored = readJson<Partial<EditorSettings>>(KEYS.editorSettings)
  if (!stored || typeof stored !== 'object') {
    return { ...DEFAULT_EDITOR_SETTINGS }
  }

  const merged: EditorSettings = { ...DEFAULT_EDITOR_SETTINGS, ...stored }

  return {
    ...merged,
    fontSize: clamp(Number(merged.fontSize) || DEFAULT_EDITOR_SETTINGS.fontSize, FONT_SIZE_RANGE.min, FONT_SIZE_RANGE.max),
    lineHeight: clamp(
      Number(merged.lineHeight) || DEFAULT_EDITOR_SETTINGS.lineHeight,
      LINE_HEIGHT_RANGE.min,
      LINE_HEIGHT_RANGE.max,
    ),
    tabSize: clamp(Number(merged.tabSize) || DEFAULT_EDITOR_SETTINGS.tabSize, TAB_SIZE_RANGE.min, TAB_SIZE_RANGE.max),
    wordWrap: merged.wordWrap === 'on' ? 'on' : 'off',
    renderWhitespace: (['none', 'boundary', 'all'] as const).includes(merged.renderWhitespace)
      ? merged.renderWhitespace
      : DEFAULT_EDITOR_SETTINGS.renderWhitespace,
    cursorStyle: (['line', 'block', 'underline'] as const).includes(merged.cursorStyle)
      ? merged.cursorStyle
      : DEFAULT_EDITOR_SETTINGS.cursorStyle,
  }
}

export function saveEditorSettings(settings: EditorSettings): boolean {
  return writeJson(KEYS.editorSettings, settings)
}

export function loadLayoutSettings(): LayoutSettings {
  const stored = readJson<Partial<LayoutSettings>>(KEYS.layoutSettings)
  if (!stored || typeof stored !== 'object') {
    return { ...DEFAULT_LAYOUT_SETTINGS }
  }

  return {
    paneOrder: stored.paneOrder === 'io-left' ? 'io-left' : 'editor-left',
    splitRatio: clamp(Number(stored.splitRatio) || DEFAULT_LAYOUT_SETTINGS.splitRatio, 0.2, 0.8),
  }
}

export function saveLayoutSettings(settings: LayoutSettings): boolean {
  return writeJson(KEYS.layoutSettings, settings)
}

export function loadTheme(): Theme {
  const stored = readJson<Theme>(KEYS.theme)
  if (stored === 'vs-light' || stored === 'vs-dark' || stored === 'system') {
    return stored
  }
  return DEFAULT_THEME
}

export function saveTheme(theme: Theme): boolean {
  return writeJson(KEYS.theme, theme)
}

/** Resolves 'system' to the OS-preferred theme via prefers-color-scheme (§16.4). */
export function resolveTheme(theme: Theme): 'vs-dark' | 'vs-light' {
  if (theme !== 'system') {
    return theme
  }
  if (typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: light)').matches) {
    return 'vs-light'
  }
  return 'vs-dark'
}

export interface StoredSource {
  schemaVersion: number
  filename: string
  content: string
  updatedAt: string
}

export const DEFAULT_SOURCE = `using System;

Console.WriteLine("Hello, codedotnet!");
`

export function loadSource(): string {
  const stored = readJson<StoredSource>(KEYS.source)
  if (stored && typeof stored.content === 'string') {
    return stored.content
  }
  return DEFAULT_SOURCE
}

export function saveSource(content: string): boolean {
  const record: StoredSource = {
    schemaVersion: STORAGE_VERSION,
    filename: 'Program.cs',
    content,
    updatedAt: new Date().toISOString(),
  }
  writeJson(KEYS.storageVersion, STORAGE_VERSION)
  return writeJson(KEYS.source, record)
}

export function loadStdin(): string {
  const stored = readJson<string>(KEYS.stdin)
  return typeof stored === 'string' ? stored : ''
}

export function saveStdin(value: string): boolean {
  return writeJson(KEYS.stdin, value)
}

export function isLocalStorageAvailable(): boolean {
  return isStorageAvailable()
}

/** Clears all codedotnet-owned localStorage keys (settings, layout, theme, source, stdin). */
export function clearAllLocalData(): void {
  if (!isStorageAvailable()) {
    return
  }
  for (const key of Object.values(KEYS)) {
    window.localStorage.removeItem(key)
  }
}
