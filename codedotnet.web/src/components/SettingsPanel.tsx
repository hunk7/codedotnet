import type { EditorSettings, Theme } from '../editor/editorSettings'

export interface SettingsPanelProps {
  open: boolean
  settings: EditorSettings
  theme: Theme
  onChange: (settings: EditorSettings) => void
  onThemeChange: (theme: Theme) => void
  onClose: () => void
}

/** Modal-style settings dialog for editor and appearance preferences (FR §16). */
export function SettingsPanel({ open, settings, theme, onChange, onThemeChange, onClose }: SettingsPanelProps) {
  if (!open) {
    return null
  }

  function update<K extends keyof EditorSettings>(key: K, value: EditorSettings[K]) {
    onChange({ ...settings, [key]: value })
  }

  return (
    <div className="settings-overlay" role="dialog" aria-modal="true" aria-label="Settings">
      <div className="settings-panel">
        <div className="settings-panel__header">
          <h2>Settings</h2>
          <button type="button" onClick={onClose} aria-label="Close settings">
            ×
          </button>
        </div>

        <div className="settings-panel__body">
          <label>
            Theme
            <select value={theme} onChange={(e) => onThemeChange(e.target.value as Theme)}>
              <option value="system">System</option>
              <option value="vs-dark">Dark</option>
              <option value="vs-light">Light</option>
            </select>
          </label>

          <label>
            Font size
            <input
              type="number"
              min={10}
              max={24}
              value={settings.fontSize}
              onChange={(e) => update('fontSize', Number(e.target.value))}
            />
          </label>

          <label>
            Line height
            <input
              type="number"
              min={14}
              max={36}
              value={settings.lineHeight}
              onChange={(e) => update('lineHeight', Number(e.target.value))}
            />
          </label>

          <label>
            Tab size
            <input
              type="number"
              min={2}
              max={8}
              value={settings.tabSize}
              onChange={(e) => update('tabSize', Number(e.target.value))}
            />
          </label>

          <label className="settings-panel__checkbox">
            <input
              type="checkbox"
              checked={settings.wordWrap === 'on'}
              onChange={(e) => update('wordWrap', e.target.checked ? 'on' : 'off')}
            />
            Word wrap
          </label>

          <label className="settings-panel__checkbox">
            <input
              type="checkbox"
              checked={settings.minimap}
              onChange={(e) => update('minimap', e.target.checked)}
            />
            Minimap
          </label>

          <label>
            Render whitespace
            <select
              value={settings.renderWhitespace}
              onChange={(e) => update('renderWhitespace', e.target.value as EditorSettings['renderWhitespace'])}
            >
              <option value="none">None</option>
              <option value="boundary">Boundary</option>
              <option value="all">All</option>
            </select>
          </label>

          <label className="settings-panel__checkbox">
            <input
              type="checkbox"
              checked={settings.stickyScroll}
              onChange={(e) => update('stickyScroll', e.target.checked)}
            />
            Sticky scroll
          </label>

          <label className="settings-panel__checkbox">
            <input
              type="checkbox"
              checked={settings.smoothScrolling}
              onChange={(e) => update('smoothScrolling', e.target.checked)}
            />
            Smooth scrolling
          </label>

          <label>
            Cursor style
            <select
              value={settings.cursorStyle}
              onChange={(e) => update('cursorStyle', e.target.value as EditorSettings['cursorStyle'])}
            >
              <option value="line">Line</option>
              <option value="block">Block</option>
              <option value="underline">Underline</option>
            </select>
          </label>

          <label className="settings-panel__checkbox">
            <input type="checkbox" checked={settings.folding} onChange={(e) => update('folding', e.target.checked)} />
            Code folding
          </label>

          <label className="settings-panel__checkbox">
            <input
              type="checkbox"
              checked={settings.autoIndent}
              onChange={(e) => update('autoIndent', e.target.checked)}
            />
            Auto indent
          </label>
        </div>
      </div>
    </div>
  )
}

export default SettingsPanel
