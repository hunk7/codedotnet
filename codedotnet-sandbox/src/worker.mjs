// Child-process worker: boots the published CodeDotNet.Engine wasm artifact once, then
// services compile_and_run requests sent over IPC until the parent kills it (hard
// timeout) or sends a 'shutdown' message.
//
// This is intentionally a *process*, not an in-process module, because the Level 0 spike
// (see docs/research/SPIKE_FINDINGS.md) proved that a synchronous infinite loop inside the
// wasm runtime blocks the Node.js event loop entirely; only OS-level process termination
// (SIGKILL) can reclaim control from a runaway user program.

import { dotnet } from '../../codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js'

let assemblyExports = null
let startupError = null

async function boot() {
  try {
    const { getAssemblyExports, getConfig } = await dotnet.create()
    const config = getConfig()
    assemblyExports = await getAssemblyExports(config.mainAssemblyName)
    process.send({ type: 'ready' })
  } catch (err) {
    startupError = err && err.message ? err.message : String(err)
    process.send({ type: 'boot-error', error: startupError })
  }
}

process.on('message', (message) => {
  if (!message || typeof message !== 'object') {
    return
  }

  if (message.type === 'shutdown') {
    process.exit(0)
    return
  }

  if (message.type !== 'compile_and_run') {
    return
  }

  const { requestId, source, stdin } = message

  if (!assemblyExports) {
    process.send({
      type: 'result',
      requestId,
      error: startupError || 'worker runtime not ready',
    })
    return
  }

  try {
    const json = assemblyExports.CodeDotNet.Compiler.CompileAndRunHost.CompileAndRun(
      source ?? '',
      stdin ?? '',
    )
    process.send({ type: 'result', requestId, payload: JSON.parse(json) })
  } catch (err) {
    process.send({
      type: 'result',
      requestId,
      error: err && err.message ? err.message : String(err),
    })
  }
})

await boot()
