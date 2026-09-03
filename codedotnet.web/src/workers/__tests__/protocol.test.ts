import { describe, it, expect } from 'vitest'
import { PROTOCOL_VERSION, type WorkerRequestEnvelope, type WorkerResponseEnvelope, type CompileAndRunRequestPayload, type CompileAndRunResponsePayload, type RuntimeInformation } from '../protocol'

describe('worker protocol shapes', () => {
  it('builds a well-formed CompileAndRun request envelope', () => {
    const payload: CompileAndRunRequestPayload = { source: 'Console.WriteLine(1);', stdin: '' }
    const envelope: WorkerRequestEnvelope<CompileAndRunRequestPayload> = {
      protocolVersion: PROTOCOL_VERSION,
      requestId: 1,
      operation: 'CompileAndRun',
      payload,
    }

    expect(envelope.protocolVersion).toBe(PROTOCOL_VERSION)
    expect(envelope.operation).toBe('CompileAndRun')
    expect(envelope.payload?.source).toContain('Console.WriteLine')
  })

  it('builds a well-formed Response envelope carrying a CompileAndRunResponsePayload', () => {
    const payload: CompileAndRunResponsePayload = {
      status: 'ExecutionCompleted',
      diagnostics: [],
      output: 'hello',
      outputTruncated: false,
      exitCode: 0,
      exceptionType: null,
      exceptionMessage: null,
      exceptionStackTrace: null,
      compilationDurationMs: 1,
      executionDurationMs: 2,
      totalDurationMs: 3,
    }
    const envelope: WorkerResponseEnvelope<CompileAndRunResponsePayload> = {
      protocolVersion: PROTOCOL_VERSION,
      requestId: 1,
      operation: 'Response',
      payload,
    }

    expect(envelope.operation).toBe('Response')
    expect(envelope.payload?.status).toBe('ExecutionCompleted')
    expect(envelope.payload?.output).toBe('hello')
  })

  it('surfaces an error on a RuntimeError envelope', () => {
    const envelope: WorkerResponseEnvelope = {
      protocolVersion: PROTOCOL_VERSION,
      requestId: null,
      operation: 'RuntimeError',
      error: 'boot failed',
    }

    expect(envelope.operation).toBe('RuntimeError')
    expect(envelope.error).toBe('boot failed')
  })

  it('includes Roslyn/language version fields on RuntimeInformation', () => {
    const info: RuntimeInformation = {
      frameworkDescription: '.NET 10.0',
      environmentVersion: '10.0.0',
      osArchitecture: 'Wasm',
      processArchitecture: 'Wasm',
      runtimeIdentifier: 'browser-wasm',
      roslynVersion: '4.14.0.0',
      languageVersion: 'CSharp13',
    }

    expect(info.roslynVersion).toBe('4.14.0.0')
    expect(info.languageVersion).toBe('CSharp13')
  })
})
