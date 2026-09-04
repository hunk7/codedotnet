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
          <div className="settings-panel__row">
            <label htmlFor="settings-theme">Theme</label>
            <select
              id="settings-theme"
              value={theme}
              onChange={(e) => onThemeChange(e.target.value as Theme)}
            >
              <option value="system">System</option>
              <option value="vs-dark">Dark</option>
              <option value="vs-light">Light</option>
            </select>
          </div>

          <div className="settings-panel__row">
            <label htmlFor="settings-font-size">Font size</label>
            <input
              id="settings-font-size"
              type="number"
              min={10}
              max={24}
              value={settings.fontSize}
              onChange={(e) => update('fontSize', Number(e.target.value))}
            />
          </div>

          <div className="settings-panel__row">
            <label htmlFor="settings-line-height">Line height</label>
            <input
              id="settings-line-height"
              type="number"
              min={14}
              max={36}
              value={settings.lineHeight}
              onChange={(e) => update('lineHeight', Number(e.target.value))}
            />
          </div>

          <div className="settings-panel__row">
            <label htmlFor="settings-tab-size">Tab size</label>
            <input
              id="settings-tab-size"
              type="number"
              min={2}
              max={8}
              value={settings.tabSize}
              onChange={(e) => update('tabSize', Number(e.target.value))}
            />
          </div>

          <div className="settings-panel__row settings-panel__row--checkbox">
            <label htmlFor="settings-word-wrap">Word wrap</label>
            <input
              id="settings-word-wrap"
              type="checkbox"
              checked={settings.wordWrap === 'on'}
              onChange={(e) => update('wordWrap', e.target.checked ? 'on' : 'off')}
            />
          </div>

          <div className="settings-panel__row settings-panel__row--checkbox">
            <label htmlFor="settings-minimap">Minimap</label>
            <input
              id="settings-minimap"
              type="checkbox"
              checked={settings.minimap}
              onChange={(e) => update('minimap', e.target.checked)}
            />
          </div>

          <div className="settings-panel__row">
            <label htmlFor="settings-render-whitespace">Render whitespace</label>
            <select
              id="settings-render-whitespace"
              value={settings.renderWhitespace}
              onChange={(e) => update('renderWhitespace', e.target.value as EditorSettings['renderWhitespace'])}
            >
              <option value="none">None</option>
              <option value="boundary">Boundary</option>
              <option value="all">All</option>
            </select>
          </div>

          <div className="settings-panel__row settings-panel__row--checkbox">
            <label htmlFor="settings-sticky-scroll">Sticky scroll</label>
            <input
              id="settings-sticky-scroll"
              type="checkbox"
              checked={settings.stickyScroll}
              onChange={(e) => update('stickyScroll', e.target.checked)}
            />
          </div>

          <div className="settings-panel__row settings-panel__row--checkbox">
            <label htmlFor="settings-smooth-scrolling">Smooth scrolling</label>
            <input
              id="settings-smooth-scrolling"
              type="checkbox"
              checked={settings.smoothScrolling}
              onChange={(e) => update('smoothScrolling', e.target.checked)}
            />
          </div>

          <div className="settings-panel__row">
            <label htmlFor="settings-cursor-style">Cursor style</label>
            <select
              id="settings-cursor-style"
              value={settings.cursorStyle}
              onChange={(e) => update('cursorStyle', e.target.value as EditorSettings['cursorStyle'])}
            >
              <option value="line">Line</option>
              <option value="block">Block</option>
              <option value="underline">Underline</option>
            </select>
          </div>

          <div className="settings-panel__row settings-panel__row--checkbox">
            <label htmlFor="settings-folding">Code folding</label>
            <input
              id="settings-folding"
              type="checkbox"
              checked={settings.folding}
              onChange={(e) => update('folding', e.target.checked)}
            />
          </div>

          <div className="settings-panel__row settings-panel__row--checkbox">
            <label htmlFor="settings-auto-indent">Auto indent</label>
            <input
              id="settings-auto-indent"
              type="checkbox"
              checked={settings.autoIndent}
              onChange={(e) => update('autoIndent', e.target.checked)}
            />
          </div>
        </div>
      </div>
    </div>
  )
}

export default SettingsPanel
