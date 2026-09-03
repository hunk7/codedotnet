import type { RuntimeInformation } from '../workers/protocol'

export interface AboutDialogProps {
  open: boolean
  runtimeInfo: RuntimeInformation | null
  onClose: () => void
}

/**
 * Displays build/version metadata and live worker runtime information (docs §18 About).
 * Version/commit/build-date are injected at build time via Vite `define` (see vite.config.ts
 * and src/globals.d.ts); Roslyn/runtime details come from the live worker's GetRuntimeInformation
 * response so they always reflect what actually executed the user's code.
 */
function AboutDialog({ open, runtimeInfo, onClose }: AboutDialogProps) {
  if (!open) {
    return null
  }

  const hostingOrigin = typeof window !== 'undefined' ? window.location.origin + window.location.pathname : 'unknown'

  return (
    <div className="settings-overlay" role="dialog" aria-modal="true" aria-label="About codedotnet">
      <div className="settings-panel">
        <div className="settings-panel__header">
          <h2>About codedotnet</h2>
          <button type="button" onClick={onClose} aria-label="Close about dialog">
            ×
          </button>
        </div>

        <div className="settings-panel__body about-dialog__body">
          <dl>
            <dt>Version</dt>
            <dd>{__APP_VERSION__}</dd>

            <dt>Commit</dt>
            <dd>{__APP_COMMIT__}</dd>

            <dt>Build date</dt>
            <dd>{new Date(__APP_BUILD_DATE__).toLocaleString()}</dd>

            <dt>Hosting origin</dt>
            <dd>{hostingOrigin}</dd>

            <dt>Execution environment</dt>
            <dd>Browser Web Worker (.NET WebAssembly)</dd>

            <dt>.NET runtime</dt>
            <dd>{runtimeInfo ? `${runtimeInfo.frameworkDescription} (${runtimeInfo.environmentVersion})` : 'Unavailable'}</dd>

            <dt>Roslyn version</dt>
            <dd>{runtimeInfo?.roslynVersion ?? 'Unavailable'}</dd>

            <dt>C# language version</dt>
            <dd>{runtimeInfo?.languageVersion ?? 'Unavailable'}</dd>

            <dt>Runtime identifier</dt>
            <dd>{runtimeInfo?.runtimeIdentifier ?? 'Unavailable'}</dd>
          </dl>
        </div>
      </div>
    </div>
  )
}

export default AboutDialog
