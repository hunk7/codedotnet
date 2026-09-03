import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import App from '../App'

vi.mock('../components/MonacoEditor', () => ({
  default: () => null,
}))

vi.mock('../workers/workerManager', () => ({
  WorkerManager: class {
    initialize() {
      return new Promise(() => {
        /* never resolves: keeps the component in its initial loading state for this test */
      })
    }
    getRuntimeInformation() {
      return Promise.resolve(null)
    }
    onStateChange() {
      return () => {}
    }
    stop() {}
  },
}))

describe('App', () => {
  it('renders the loading status while the worker initializes', () => {
    render(<App />)
    expect(screen.getByText('Loading .NET runtime and C# compiler...')).toBeInTheDocument()
  })
})

describe('App shell once ready', () => {
  it('renders Run/Stop/Clear/Reset/Download/Settings/About controls once the worker is ready', async () => {
    vi.resetModules()
    vi.doMock('../workers/workerManager', () => ({
      WorkerManager: class {
        initialize() {
          return Promise.resolve()
        }
        getRuntimeInformation() {
          return Promise.resolve({
            frameworkDescription: '.NET 10.0',
            environmentVersion: '10.0.0',
            osArchitecture: 'Wasm',
            processArchitecture: 'Wasm',
            runtimeIdentifier: 'browser-wasm',
            roslynVersion: '4.14.0.0',
            languageVersion: 'CSharp13',
          })
        }
        onStateChange(cb: (state: string) => void) {
          cb('Ready')
          return () => {}
        }
        stop() {}
      },
    }))

    const { default: ReadyApp } = await import('../App')
    render(<ReadyApp />)

    await waitFor(() => expect(screen.getByText('Run')).toBeInTheDocument())
    expect(screen.getByText('Stop')).toBeInTheDocument()
    expect(screen.getByText('Clear')).toBeInTheDocument()
    expect(screen.getByText('Reset')).toBeInTheDocument()
    expect(screen.getByText('Download')).toBeInTheDocument()
    expect(screen.getByText('Settings')).toBeInTheDocument()
    expect(screen.getByText('About')).toBeInTheDocument()
  })
})
