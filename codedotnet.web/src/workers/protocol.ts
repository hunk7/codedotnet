export const PROTOCOL_VERSION = 1

export type WorkerOperation = 'GetRuntimeInformation'

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
}
