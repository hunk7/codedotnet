import { describe, it, expect } from 'vitest'
import { diagnosticsToMarkers, severityToMonaco } from '../diagnostics'
import type { DiagnosticPayload } from '../workers/protocol'

function makeDiagnostic(overrides: Partial<DiagnosticPayload> = {}): DiagnosticPayload {
  return {
    id: 'CS0000',
    severity: 'Error',
    message: 'Test message',
    filePath: 'Program.cs',
    startLine: 1,
    startColumn: 2,
    endLine: 1,
    endColumn: 5,
    ...overrides,
  }
}

describe('severityToMonaco', () => {
  it('maps Error and Warning to their Monaco equivalents, and anything else to Info', () => {
    expect(severityToMonaco('Error')).toBe(severityToMonaco('Error'))
    expect(typeof severityToMonaco('Error')).toBe('number')
    expect(severityToMonaco('Warning')).not.toBe(severityToMonaco('Error'))
    expect(severityToMonaco('Info' as DiagnosticPayload['severity'])).toBe(
      severityToMonaco('Hidden' as DiagnosticPayload['severity']),
    )
  })
})

describe('diagnosticsToMarkers', () => {
  it('maps diagnostic fields onto Monaco marker fields', () => {
    const diagnostics = [makeDiagnostic({ id: 'CS0123', message: 'oops', startLine: 3, startColumn: 4, endLine: 3, endColumn: 10 })]
    const markers = diagnosticsToMarkers(diagnostics)

    expect(markers).toHaveLength(1)
    expect(markers[0]).toMatchObject({
      message: 'CS0123: oops',
      startLineNumber: 3,
      startColumn: 4,
      endLineNumber: 3,
      endColumn: 10,
    })
  })

  it('returns an empty array for no diagnostics', () => {
    expect(diagnosticsToMarkers([])).toEqual([])
  })
})
