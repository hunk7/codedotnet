import * as monaco from '../node_modules/monaco-editor/esm/vs/editor/editor.api.js'
import type { DiagnosticPayload } from './workers/protocol'

/** Maps a compiler diagnostic severity to the corresponding Monaco marker severity. */
export function severityToMonaco(severity: DiagnosticPayload['severity']): monaco.MarkerSeverity {
  switch (severity) {
    case 'Error':
      return monaco.MarkerSeverity.Error
    case 'Warning':
      return monaco.MarkerSeverity.Warning
    default:
      return monaco.MarkerSeverity.Info
  }
}

/** Converts a list of compiler diagnostics into Monaco marker data for the editor. */
export function diagnosticsToMarkers(
  diagnostics: DiagnosticPayload[],
): monaco.editor.IMarkerData[] {
  return diagnostics.map((d) => ({
    severity: severityToMonaco(d.severity),
    message: `${d.id}: ${d.message}`,
    startLineNumber: d.startLine,
    startColumn: d.startColumn,
    endLineNumber: d.endLine,
    endColumn: d.endColumn,
  }))
}
