import { describe, it, expect, beforeEach } from 'vitest'
import {
  loadEditorSettings,
  saveEditorSettings,
  loadLayoutSettings,
  saveLayoutSettings,
  loadTheme,
  saveTheme,
  loadStdin,
  saveStdin,
  loadSource,
  saveSource,
  resolveTheme,
  clearAllLocalData,
  DEFAULT_EDITOR_SETTINGS,
  DEFAULT_LAYOUT_SETTINGS,
  DEFAULT_SOURCE,
} from '../editor/editorSettings'

beforeEach(() => {
  window.localStorage.clear()
})

describe('editorSettings persistence and validation', () => {
  it('returns defaults when nothing is stored', () => {
    expect(loadEditorSettings()).toEqual(DEFAULT_EDITOR_SETTINGS)
    expect(loadLayoutSettings()).toEqual(DEFAULT_LAYOUT_SETTINGS)
    expect(loadTheme()).toBe('system')
    expect(loadStdin()).toBe('')
    expect(loadSource()).toBe(DEFAULT_SOURCE)
  })

  it('round-trips valid editor settings', () => {
    const custom = { ...DEFAULT_EDITOR_SETTINGS, fontSize: 18, wordWrap: 'on' as const }
    saveEditorSettings(custom)
    expect(loadEditorSettings()).toEqual(custom)
  })

  it('clamps out-of-range editor settings on load', () => {
    window.localStorage.setItem(
      'codedotnet.editor.settings',
      JSON.stringify({ ...DEFAULT_EDITOR_SETTINGS, fontSize: 999, tabSize: -5 }),
    )
    const loaded = loadEditorSettings()
    expect(loaded.fontSize).toBeLessThanOrEqual(24)
    expect(loaded.tabSize).toBeGreaterThanOrEqual(2)
  })

  it('falls back to defaults for invalid enum-like fields', () => {
    window.localStorage.setItem(
      'codedotnet.editor.settings',
      JSON.stringify({ ...DEFAULT_EDITOR_SETTINGS, wordWrap: 'nonsense', cursorStyle: 'nonsense' }),
    )
    const loaded = loadEditorSettings()
    expect(loaded.wordWrap).toBe('off')
    expect(loaded.cursorStyle).toBe(DEFAULT_EDITOR_SETTINGS.cursorStyle)
  })

  it('falls back to defaults when stored JSON is malformed', () => {
    window.localStorage.setItem('codedotnet.editor.settings', '{not json')
    expect(loadEditorSettings()).toEqual(DEFAULT_EDITOR_SETTINGS)
  })

  it('round-trips layout settings and clamps split ratio', () => {
    saveLayoutSettings({ paneOrder: 'io-left', splitRatio: 0.99 })
    const loaded = loadLayoutSettings()
    expect(loaded.paneOrder).toBe('io-left')
    expect(loaded.splitRatio).toBeLessThanOrEqual(0.8)
  })

  it('round-trips theme and rejects invalid values', () => {
    saveTheme('vs-light')
    expect(loadTheme()).toBe('vs-light')

    window.localStorage.setItem('codedotnet.theme', JSON.stringify('bogus'))
    expect(loadTheme()).toBe('system')
  })

  it('resolves concrete themes and falls back for system based on matchMedia', () => {
    expect(resolveTheme('vs-dark')).toBe('vs-dark')
    expect(resolveTheme('vs-light')).toBe('vs-light')
    expect(['vs-dark', 'vs-light']).toContain(resolveTheme('system'))
  })

  it('round-trips stdin and source', () => {
    saveStdin('hello')
    expect(loadStdin()).toBe('hello')

    saveSource('Console.WriteLine(1);')
    expect(loadSource()).toBe('Console.WriteLine(1);')
  })

  it('clears all persisted keys via clearAllLocalData', () => {
    saveStdin('x')
    saveSource('y')
    saveTheme('vs-light')
    clearAllLocalData()

    expect(loadStdin()).toBe('')
    expect(loadSource()).toBe(DEFAULT_SOURCE)
    expect(loadTheme()).toBe('system')
  })
})
