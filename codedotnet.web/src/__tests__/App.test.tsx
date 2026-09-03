import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import App from '../App'

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
    stop() {}
  },
}))

describe('App', () => {
  it('renders the codedotnet brand and loading status', () => {
    render(<App />)
    expect(screen.getByText('codedotnet')).toBeInTheDocument()
    expect(screen.getByText('Loading .NET runtime...')).toBeInTheDocument()
  })
})
