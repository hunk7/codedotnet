export const PROTOCOL_VERSION = 1

export type WorkerOperation = 'GetRuntimeInformation' | 'CompileAndRun'

export type WorkerEvent = 'RuntimeReady' | 'RuntimeError' | 'Response'

export interface WorkerRequestEnvelope<TPayload = unknown> {
  protocolVersion: number
  requestId: number | null
  operation: WorkerOperation
  payload?: TPayload
}

export interface WorkerResponseEnvelope<TPayload = unknown> {
  protocolVersion: number
  requestId: number | null
  operation: WorkerEvent
  payload?: TPayload
  error?: string
}

export interface RuntimeInformation {
  frameworkDescription: string
  environmentVersion: string
  osArchitecture: string
  processArchitecture: string
  runtimeIdentifier: string
  roslynVersion: string
  languageVersion: string
}

export interface CompileAndRunRequestPayload {
  source: string
  stdin: string
}

export type ExecutionStatus =
  | 'BuildFailed'
  | 'ExecutionCompleted'
  | 'ExecutionCompletedWithWarnings'
  | 'ExecutionFailed'

export interface DiagnosticPayload {
  id: string
  severity: 'Error' | 'Warning' | 'Info'
  message: string
  filePath: string | null
  startLine: number
  startColumn: number
  endLine: number
  endColumn: number
}

export interface CompileAndRunResponsePayload {
  status: ExecutionStatus
  diagnostics: DiagnosticPayload[]
  output: string
  outputTruncated: boolean
  exitCode: number | null
  exceptionType: string | null
  exceptionMessage: string | null
  exceptionStackTrace: string | null
  compilationDurationMs: number
  executionDurationMs: number
  totalDurationMs: number
}

