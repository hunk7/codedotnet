import { useEffect, useState } from 'react'
import './App.css'
import { WorkerManager } from './workers/workerManager'
import type { RuntimeInformation } from './workers/protocol'

function App() {
  const [status, setStatus] = useState('Loading development environment...')
  const [runtimeInfo, setRuntimeInfo] = useState<RuntimeInformation | null>(null)

  useEffect(() => {
    const manager = new WorkerManager()
    let cancelled = false

    async function boot() {
      try {
        setStatus('Loading .NET runtime...')
        await manager.initialize()
        if (cancelled) return

        setStatus('Loading C# compiler...')
        const info = await manager.getRuntimeInformation()
        if (cancelled) return

        setRuntimeInfo(info)
        setStatus('Ready')
      } catch (err) {
        if (!cancelled) {
          setStatus(`Failed to initialize: ${(err as Error).message}`)
        }
      }
    }

    void boot()

    return () => {
      cancelled = true
      manager.stop()
    }
  }, [])

  return (
    <div className="app-shell">
      <header className="app-shell__brand">
        <span className="app-shell__logo">codedotnet</span>
      </header>
      <main className="app-shell__status">
        <p>{status}</p>
        {runtimeInfo && (
          <pre className="app-shell__runtime-info">{JSON.stringify(runtimeInfo, null, 2)}</pre>
        )}
      </main>
    </div>
  )
}

export default App
