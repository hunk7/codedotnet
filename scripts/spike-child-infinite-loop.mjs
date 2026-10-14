// Child process used by scripts/spike-node-child-timeout.mjs.
// Boots the wasm runtime and invokes CompileAndRun with an infinite loop program,
// simulating a runaway user submission that must be killed externally.

import { dotnet } from '../codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js'

console.log('[child] booting wasm runtime...')
const { getAssemblyExports, getConfig } = await dotnet.create()
const config = getConfig()
const assemblyExports = await getAssemblyExports(config.mainAssemblyName)
console.log('[child] wasm runtime ready, invoking infinite loop program...')

const source = `
using System;
class Program {
  static void Main() {
    while (true) { }
  }
}`

// This call is expected to never return; the parent process will SIGKILL us.
assemblyExports.CodeDotNet.Compiler.CompileAndRunHost.CompileAndRun(source, '')

console.log('[child] UNEXPECTED: CompileAndRun returned')
