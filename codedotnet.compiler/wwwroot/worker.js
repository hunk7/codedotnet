// Licensed under the MIT license. Adapted from Microsoft's DotNetOnWebWorkersReact sample
// (dotnet/blazor-samples, 10.0/DotNetOnWebWorkersReact/dotnet/wwwroot/worker.js) to fit the
// codedotnet typed message envelope and worker lifecycle model.

import { dotnet } from './_framework/dotnet.js'

let assemblyExports = null
let startupError = undefined

try {
  const { getAssemblyExports, getConfig } = await dotnet.create()
  const config = getConfig()
  assemblyExports = await getAssemblyExports(config.mainAssemblyName)
  self.postMessage({
    protocolVersion: 1,
    requestId: null,
    operation: 'RuntimeReady',
    payload: null,
  })
} catch (err) {
  startupError = err && err.message ? err.message : String(err)
  self.postMessage({
    protocolVersion: 1,
    requestId: null,
    operation: 'RuntimeError',
    error: startupError,
    payload: null,
  })
}

self.addEventListener(
  'message',
  async function (event) {
    const { requestId, operation, payload: requestPayload } = event.data

    try {
      if (!assemblyExports) {
        throw new Error(startupError || 'worker exports not loaded')
      }

      let payload = null

      switch (operation) {
        case 'GetRuntimeInformation': {
          const json = assemblyExports.CodeDotNet.Compiler.RuntimeInfo.GetRuntimeInformation()
          payload = JSON.parse(json)
          break
        }
        case 'CompileAndRun': {
          const source = (requestPayload && requestPayload.source) || ''
          const stdin = (requestPayload && requestPayload.stdin) || ''
          const json = assemblyExports.CodeDotNet.Compiler.CompileAndRunHost.CompileAndRun(source, stdin)
          payload = JSON.parse(json)
          break
        }
        default:
          throw new Error('Unknown operation: ' + operation)
      }

      self.postMessage({
        protocolVersion: 1,
        requestId,
        operation: 'Response',
        payload,
      })
    } catch (err) {
      self.postMessage({
        protocolVersion: 1,
        requestId,
        operation: 'Response',
        error: err && err.message ? err.message : String(err),
      })
    }
  },
  false,
)
