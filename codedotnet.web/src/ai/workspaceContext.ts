// Shared "what the user is working on" context. App owns the editor source and the last run (from the
// browser worker or the MCP sandbox); the AI tab only reads it, so all three tabs describe the same program.

export interface AiLastRun {
  origin: 'editor' | 'mcp'
  status: string
  output: string
  exceptionType?: string | null
  exceptionMessage?: string | null
  diagnosticCount: number
}

export interface AiWorkspaceContext {
  source: string
  lastRun: AiLastRun | null
}

// Mirror the service limits (question 8000 chars, focus 1500 chars).
export const MAX_QUESTION_CHARS = 8000
export const MAX_FOCUS_CHARS = 1500

function clip(text: string, max: number): string {
  return text.length <= max ? text : text.slice(0, Math.max(0, max - 1)) + '…'
}

export function describeRun(run: AiLastRun, maxOutput: number): string {
  const lines = [
    `Last run (${run.origin === 'mcp' ? 'MCP sandbox' : 'editor'}): ${run.status}, ${run.diagnosticCount} diagnostic(s)`,
  ]
  if (run.exceptionType)
    lines.push(`Exception: ${run.exceptionType}: ${run.exceptionMessage ?? ''}`)
  if (run.output.trim()) lines.push(`Output: ${clip(run.output.trim(), maxOutput)}`)
  return lines.join('\n')
}

/** Short description of the last run, sized for the service's `focus` field. */
export function buildRunFocus(run: AiLastRun | null): string | undefined {
  return run ? clip(describeRun(run, 1000), MAX_FOCUS_CHARS) : undefined
}

/** Prefixes a free-form question with the current code and last run, within the question size limit. */
export function buildContextQuestion(question: string, context: AiWorkspaceContext): string {
  const parts = [question.trim()]
  if (context.lastRun) parts.push(describeRun(context.lastRun, 800))
  const used = parts.join('\n\n').length
  const fenceOverhead = '\n\nCurrent editor code (C#):\n```csharp\n\n```'.length + 20
  const budget = MAX_QUESTION_CHARS - used - fenceOverhead
  if (context.source.trim() && budget >= 200) {
    parts.push('Current editor code (C#):\n```csharp\n' + clip(context.source, budget) + '\n```')
  }
  return parts.join('\n\n')
}
